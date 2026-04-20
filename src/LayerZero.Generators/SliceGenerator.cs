using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Generators;

/// <summary>
/// Generates LayerZero registration extensions and messaging manifests.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SliceGenerator : IIncrementalGenerator
{
    private const string SliceExtensionsClassNamePrefix = "LayerZeroGeneratedSliceExtensions";
    private const string SliceExtensionsNamespace = "LayerZero.AspNetCore";
    private const string SliceRegistrarClassNamePrefix = "LayerZeroGeneratedSliceRegistrar";
    private const string SliceModuleInitializerClassNamePrefix = "LayerZeroGeneratedSliceModuleInitializer";
    private const string MessagingNamespace = "LayerZero.Messaging";
    private const string MessagingRegistrarClassNamePrefix = "LayerZeroGeneratedMessagingRegistrar";
    private const string MessagingModuleInitializerClassName = "LayerZeroGeneratedMessagingModuleInitializer";
    private const string MessagingRegistryClassName = "LayerZeroGeneratedMessageRegistry";
    private const string MessagingTopologyManifestClassName = "LayerZeroGeneratedMessageTopologyManifest";
    private const string MessagingJsonContextClassName = "LayerZeroGeneratedMessageJsonContext";
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly DiagnosticDescriptor InvalidSliceShape = new(
        "LZGEN001",
        "Invalid endpoint slice shape",
        "Endpoint slice '{0}' must be a non-generic static class with a public or internal static void MapEndpoint(IEndpointRouteBuilder endpoints) method",
        "LayerZero.Slices",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ExtensionCollision = new(
        "LZGEN002",
        "Generated slice extension collision",
        "The type '{0}.{1}' already exists; rename it so LayerZero can generate extensions",
        "LayerZero.Slices",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedHandlerShape = new(
        "LZGEN003",
        "Unsupported handler registration shape",
        "Slice service '{0}' implements a LayerZero registration interface but is abstract, generic, or not a concrete class",
        "LayerZero.Slices",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PartialSliceModule = new(
        "LZGEN004",
        "Partial endpoint slice modules are not supported",
        "Endpoint slice module '{0}' must not be partial. Use one static class per HTTP slice.",
        "LayerZero.Slices",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateCommandHandler = new(
        "LZGEN005",
        "Duplicate command handlers are not supported for messaging",
        "Command '{0}' has multiple ICommandHandler<{0}> implementations: {1}",
        "LayerZero.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMessageName = new(
        "LZGEN006",
        "Duplicate logical message names are not supported",
        "Messages '{0}' and '{1}' resolve to the same logical name '{2}'",
        "LayerZero.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidAffinityKey = new(
        "LZGEN007",
        "Invalid affinity key declaration",
        "Message '{0}' declares affinity member '{1}', but no readable instance property or field with that name exists",
        "LayerZero.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (syntaxContext, _) => GetTypeSymbol(syntaxContext))
            .Where(static symbol => symbol is not null);

        var source = context.CompilationProvider.Combine(candidateTypes.Collect());
        context.RegisterSourceOutput(source, static (sourceContext, value) =>
        {
            Execute(sourceContext, value.Left, value.Right);
        });
    }

    private static INamedTypeSymbol? GetTypeSymbol(GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> candidateTypes)
    {
        var sliceExtensionsAvailable = compilation.GetTypeByMetadataName("LayerZero.AspNetCore.ServiceCollectionExtensions") is not null;
        var messagingAvailable = compilation.GetTypeByMetadataName("LayerZero.Messaging.IMessageRegistry") is not null;
        var sliceExtensionsClassName = CreateGeneratedTypeName(SliceExtensionsClassNamePrefix, compilation.AssemblyName);
        var sliceRegistrarClassName = CreateGeneratedTypeName(SliceRegistrarClassNamePrefix, compilation.AssemblyName);
        var sliceModuleInitializerClassName = CreateGeneratedTypeName(SliceModuleInitializerClassNamePrefix, compilation.AssemblyName);
        var messagingRegistrarClassName = CreateGeneratedTypeName(MessagingRegistrarClassNamePrefix, compilation.AssemblyName);

        if (sliceExtensionsAvailable && HasGeneratedExtensionCollision(compilation, SliceExtensionsNamespace, sliceExtensionsClassName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ExtensionCollision,
                Location.None,
                SliceExtensionsNamespace,
                sliceExtensionsClassName));
            return;
        }

        var endpointSlices = new List<string>();
        var registrations = new List<Registration>();
        var messageState = new MessageGenerationState();

        foreach (var symbol in candidateTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            if (HasMapEndpointCandidate(symbol))
            {
                if (!IsValidHttpSliceModule(symbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidSliceShape,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString()));
                    continue;
                }

                if (IsPartial(symbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        PartialSliceModule,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString()));
                    continue;
                }

                endpointSlices.Add(symbol.ToDisplayString(FullyQualifiedFormat));
            }

            var discoveredRegistrations = DiscoverRegistrations(symbol);
            if (discoveredRegistrations.Count > 0)
            {
                if (!IsConcrete(symbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedHandlerShape,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString()));
                    continue;
                }

                registrations.Add(new Registration(
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    RegistrationKind.TryAdd));

                registrations.AddRange(discoveredRegistrations);
            }

            if (messagingAvailable)
            {
                DiscoverMessageArtifacts(context, messageState, symbol);
            }
        }

        if (sliceExtensionsAvailable)
        {
            var sliceSource = RenderSliceSource(
                endpointSlices.Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal),
                registrations.Distinct().OrderBy(static registration => registration.ServiceType, StringComparer.Ordinal)
                    .ThenBy(static registration => registration.ImplementationType, StringComparer.Ordinal)
                    .ThenBy(static registration => registration.Kind),
                sliceExtensionsClassName,
                sliceRegistrarClassName,
                sliceModuleInitializerClassName);

            context.AddSource("LayerZero.Slices.g.cs", SourceText.From(sliceSource, Encoding.UTF8));
        }

        if (!messagingAvailable)
        {
            return;
        }

        foreach (var duplicate in messageState.FindDuplicateMessageNames())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateMessageName,
                duplicate.LeftLocation,
                duplicate.LeftType,
                duplicate.RightType,
                duplicate.MessageName));
        }

        foreach (var duplicateHandler in messageState.FindDuplicateCommandHandlers())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateCommandHandler,
                duplicateHandler.Location,
                duplicateHandler.MessageType,
                string.Join(", ", duplicateHandler.HandlerTypes)));
        }

        var messagingRegistrarTypes = GetReferencedMessagingRegistrarTypes(compilation)
            .Append($"global::{MessagingNamespace}.{messagingRegistrarClassName}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        var messagingSource = RenderMessagingSource(
            registrations.Distinct().OrderBy(static registration => registration.ServiceType, StringComparer.Ordinal)
                .ThenBy(static registration => registration.ImplementationType, StringComparer.Ordinal)
                .ThenBy(static registration => registration.Kind),
            messageState,
            messagingRegistrarTypes,
            messagingRegistrarClassName);

        context.AddSource("LayerZero.Messaging.g.cs", SourceText.From(messagingSource, Encoding.UTF8));
    }

    private static void DiscoverMessageArtifacts(SourceProductionContext context, MessageGenerationState state, INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType && !symbol.IsAbstract && !symbol.IsStatic)
        {
            if (TryCreateMessageDefinition(symbol, out var message, out var invalidAffinityMember))
            {
                state.MessagesByType[message.TypeName] = message;
            }

            if (invalidAffinityMember is { } invalidAffinity)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidAffinityKey,
                    invalidAffinity.Location,
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    invalidAffinity.MemberName));
            }
        }

        foreach (var interfaceType in symbol.AllInterfaces)
        {
            if (IsInterface(interfaceType, "LayerZero.Validation", "IValidator", 1))
            {
                EnsureMessageDefinition(context, state, interfaceType.TypeArguments[0] as INamedTypeSymbol);
                var messageTypeName = interfaceType.TypeArguments[0].ToDisplayString(FullyQualifiedFormat);
                state.ValidatorTypesByMessageType.GetOrAdd(messageTypeName).Add(symbol.ToDisplayString(FullyQualifiedFormat));
                continue;
            }

            if (IsInterface(interfaceType, "LayerZero.Core", "ICommandHandler", 1))
            {
                EnsureMessageDefinition(context, state, interfaceType.TypeArguments[0] as INamedTypeSymbol);
                var commandTypeName = interfaceType.TypeArguments[0].ToDisplayString(FullyQualifiedFormat);
                state.CommandHandlerTypesByMessageType.GetOrAdd(commandTypeName).Add(new HandlerDefinition(
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    RequiresIdempotency(symbol)));
                continue;
            }

            if (IsInterface(interfaceType, "LayerZero.Core", "IEventHandler", 1))
            {
                EnsureMessageDefinition(context, state, interfaceType.TypeArguments[0] as INamedTypeSymbol);
                var eventTypeName = interfaceType.TypeArguments[0].ToDisplayString(FullyQualifiedFormat);
                state.EventHandlerTypesByMessageType.GetOrAdd(eventTypeName).Add(new HandlerDefinition(
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    RequiresIdempotency(symbol)));
            }
        }
    }

    private static void EnsureMessageDefinition(
        SourceProductionContext context,
        MessageGenerationState state,
        INamedTypeSymbol? messageSymbol)
    {
        if (messageSymbol is null)
        {
            return;
        }

        if (state.MessagesByType.ContainsKey(messageSymbol.ToDisplayString(FullyQualifiedFormat)))
        {
            return;
        }

        if (TryCreateMessageDefinition(messageSymbol, out var message, out var invalidAffinityMember))
        {
            state.MessagesByType[message.TypeName] = message;
        }

        if (invalidAffinityMember is { } invalidAffinity)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidAffinityKey,
                invalidAffinity.Location,
                messageSymbol.ToDisplayString(FullyQualifiedFormat),
                invalidAffinity.MemberName));
        }
    }

    private static bool TryCreateMessageDefinition(
        INamedTypeSymbol symbol,
        out MessageDefinition message,
        out InvalidAffinityMemberDefinition? invalidAffinityMember)
    {
        message = default;
        invalidAffinityMember = null;
        var affinityMemberName = GetAffinityMemberName(symbol, out invalidAffinityMember);

        if (ImplementsInterface(symbol, "LayerZero.Core", "ICommand", 0))
        {
            message = new MessageDefinition(
                symbol.ToDisplayString(FullyQualifiedFormat),
                GetLogicalMessageName(symbol),
                GeneratedMessageKind.Command,
                symbol.Locations.FirstOrDefault(),
                RequiresIdempotency(symbol),
                affinityMemberName);
            return true;
        }

        if (ImplementsInterface(symbol, "LayerZero.Core", "IEvent", 0))
        {
            message = new MessageDefinition(
                symbol.ToDisplayString(FullyQualifiedFormat),
                GetLogicalMessageName(symbol),
                GeneratedMessageKind.Event,
                symbol.Locations.FirstOrDefault(),
                RequiresIdempotency(symbol),
                affinityMemberName);
            return true;
        }

        return false;
    }

    private static string GetLogicalMessageName(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!string.Equals(
                attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat),
                "global::LayerZero.Messaging.MessageNameAttribute",
                StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string name
                && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static bool RequiresIdempotency(ISymbol symbol)
    {
        return symbol
            .GetAttributes()
            .Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat) is
                    "global::LayerZero.Messaging.IdempotentMessageAttribute"
                    or "global::LayerZero.Messaging.IdempotentHandlerAttribute");
    }

    private static string? GetAffinityMemberName(
        INamedTypeSymbol symbol,
        out InvalidAffinityMemberDefinition? invalidAffinityMember)
    {
        invalidAffinityMember = null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (!string.Equals(
                attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat),
                "global::LayerZero.Messaging.AffinityKeyAttribute",
                StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length != 1
                || attribute.ConstructorArguments[0].Value is not string memberName
                || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            var member = symbol.GetMembers(memberName).FirstOrDefault(static candidate =>
                candidate is IPropertySymbol property && !property.IsStatic && property.GetMethod is not null
                || candidate is IFieldSymbol field && !field.IsStatic);

            if (member is null)
            {
                invalidAffinityMember = new InvalidAffinityMemberDefinition(memberName, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? symbol.Locations.FirstOrDefault());
                return null;
            }

            return memberName;
        }

        return null;
    }

    private static bool ImplementsInterface(INamedTypeSymbol symbol, string @namespace, string name, int arity)
    {
        return symbol.AllInterfaces.Any(interfaceType => IsInterface(interfaceType, @namespace, name, arity));
    }

    private static List<Registration> DiscoverRegistrations(INamedTypeSymbol symbol)
    {
        var registrations = new List<Registration>();
        var implementationType = symbol.ToDisplayString(FullyQualifiedFormat);

        foreach (var interfaceType in symbol.AllInterfaces)
        {
            if (IsInterface(interfaceType, "LayerZero.Validation", "IValidator", 1)
                || IsInterface(interfaceType, "LayerZero.Core", "IEventHandler", 1))
            {
                registrations.Add(new Registration(
                    interfaceType.ToDisplayString(FullyQualifiedFormat),
                    implementationType,
                    RegistrationKind.TryAddEnumerable));
                continue;
            }

            if (IsLayerZeroSingleHandlerInterface(interfaceType))
            {
                registrations.Add(new Registration(
                    interfaceType.ToDisplayString(FullyQualifiedFormat),
                    implementationType,
                    RegistrationKind.TryAdd));
            }
        }

        return registrations;
    }

    private static bool IsLayerZeroSingleHandlerInterface(INamedTypeSymbol interfaceType)
    {
        return IsInterface(interfaceType, "LayerZero.Core", "IRequestHandler", 2)
            || IsInterface(interfaceType, "LayerZero.Core", "IAsyncRequestHandler", 2)
            || IsInterface(interfaceType, "LayerZero.Core", "ICommandHandler", 1)
            || IsInterface(interfaceType, "LayerZero.Core", "ICommandHandler", 2);
    }

    private static bool IsInterface(INamedTypeSymbol interfaceType, string @namespace, string name, int arity)
    {
        return interfaceType.ContainingNamespace.ToDisplayString().Equals(@namespace, StringComparison.Ordinal)
            && interfaceType.Name.Equals(name, StringComparison.Ordinal)
            && interfaceType.TypeArguments.Length == arity;
    }

    private static bool IsConcrete(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind is TypeKind.Class or TypeKind.Struct
            && !symbol.IsStatic
            && !symbol.IsAbstract
            && !symbol.IsGenericType
            && !symbol.IsUnboundGenericType;
    }

    private static bool HasMapEndpointCandidate(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers("MapEndpoint").OfType<IMethodSymbol>().Any();
    }

    private static bool IsValidHttpSliceModule(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind == TypeKind.Class
            && symbol.IsStatic
            && !symbol.IsGenericType
            && !symbol.IsUnboundGenericType
            && symbol.GetMembers("MapEndpoint")
                .OfType<IMethodSymbol>()
                .Any(static method =>
                    method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                    && method.IsStatic
                    && method.ReturnsVoid
                    && method.Parameters.Length == 1
                    && IsEndpointRouteBuilder(method.Parameters[0].Type));
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static bool IsEndpointRouteBuilder(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            && namedType.ContainingNamespace.ToDisplayString().Equals("Microsoft.AspNetCore.Routing", StringComparison.Ordinal)
            && namedType.Name.Equals("IEndpointRouteBuilder", StringComparison.Ordinal);
    }

    private static bool HasGeneratedExtensionCollision(Compilation compilation, string @namespace, string typeName)
    {
        var namespaceParts = @namespace.Split('.');
        var current = compilation.GlobalNamespace;

        foreach (var part in namespaceParts)
        {
            current = current?.GetNamespaceMembers().FirstOrDefault(namespaceSymbol => namespaceSymbol.Name.Equals(part, StringComparison.Ordinal));
            if (current is null)
            {
                return false;
            }
        }

        return current.GetTypeMembers(typeName).Any();
    }

    private static IEnumerable<string> GetReferencedMessagingRegistrarTypes(Compilation compilation)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "LayerZero.Messaging"
                    || !attribute.AttributeClass.Name.Equals("MessagingAssemblyRegistrarAttribute", StringComparison.Ordinal)
                    || attribute.ConstructorArguments.Length != 1)
                {
                    continue;
                }

                if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol registrarType)
                {
                    yield return registrarType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
            }
        }
    }

    private static string RenderSliceSource(
        IEnumerable<string> endpointSlices,
        IEnumerable<Registration> registrations,
        string sliceExtensionsClassName,
        string sliceRegistrarClassName,
        string sliceModuleInitializerClassName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine();
        builder.AppendLine($"[assembly: global::LayerZero.AspNetCore.AspNetCoreAssemblyRegistrarAttribute(typeof(global::{SliceExtensionsNamespace}.{sliceRegistrarClassName}))]");
        builder.AppendLine();
        builder.AppendLine($"namespace {SliceExtensionsNamespace}");
        builder.AppendLine("{");
        builder.AppendLine($"    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"LayerZero.Generators\", \"0.1.0-alpha.1\")]");
        builder.AppendLine($"    public static partial class {sliceExtensionsClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddSlices(");
        builder.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");

        foreach (var registration in registrations)
        {
            AppendRegistration(builder, registration);
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapSlices(");
        builder.AppendLine("            this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(endpoints);");

        foreach (var endpointSlice in endpointSlices)
        {
            builder.AppendLine();
            builder.AppendLine($"            {endpointSlice}.MapEndpoint(endpoints);");
        }

        builder.AppendLine();
        builder.AppendLine("            return endpoints;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"    public sealed class {sliceRegistrarClassName} : global::LayerZero.AspNetCore.IAspNetCoreAssemblyRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        public void Register(global::LayerZero.AspNetCore.AspNetCoreAssemblyRegistrationBuilder builder)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(builder);");

        foreach (var registration in registrations)
        {
            builder.AppendLine($"            builder.Add<{registration.ServiceType}, {registration.ImplementationType}>(global::LayerZero.AspNetCore.RegistrationKind.{registration.Kind});");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    internal static class {sliceModuleInitializerClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Initialize()");
        builder.AppendLine("        {");
        builder.AppendLine($"            global::LayerZero.AspNetCore.AspNetCoreAssemblyRegistrarCatalog.Register<global::{SliceExtensionsNamespace}.{sliceRegistrarClassName}>();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("#pragma warning restore CS1591");
        return builder.ToString();
    }

    private static string RenderMessagingSource(
        IEnumerable<Registration> registrations,
        MessageGenerationState state,
        IEnumerable<string> registrarTypes,
        string messagingRegistrarClassName)
    {
        var handledInvocations = state.GetHandledInvocations().ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine();
        builder.AppendLine($"[assembly: global::LayerZero.Messaging.MessagingAssemblyRegistrarAttribute(typeof(global::{MessagingNamespace}.{messagingRegistrarClassName}))]");
        builder.AppendLine();
        builder.AppendLine($"namespace {MessagingNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"    public sealed class {messagingRegistrarClassName} : global::LayerZero.Messaging.IMessagingAssemblyRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        public void Register(global::LayerZero.Messaging.MessagingAssemblyRegistrationBuilder builder)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(builder);");

        foreach (var registration in registrations)
        {
            builder.AppendLine($"            builder.AddService<{registration.ServiceType}, {registration.ImplementationType}>(global::LayerZero.Messaging.MessagingRegistrationKind.{registration.Kind});");
        }

        builder.AppendLine($"            builder.AddRegistry<{MessagingRegistryClassName}>();");
        builder.AppendLine($"            builder.AddTopologyManifest<{MessagingTopologyManifestClassName}>();");

        foreach (var invocation in handledInvocations)
        {
            builder.AppendLine($"            builder.AddInvoker<{GetInvokerClassName(invocation.Message.TypeName, invocation.Handler.ImplementationType)}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    internal static class {MessagingModuleInitializerClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Initialize()");
        builder.AppendLine("        {");

        foreach (var registrarType in registrarTypes)
        {
            builder.AppendLine($"            global::LayerZero.Messaging.MessagingAssemblyRegistrarCatalog.Register<{registrarType}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();

        builder.AppendLine($"    internal sealed class {MessagingRegistryClassName} : global::LayerZero.Messaging.IMessageRegistry");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly global::LayerZero.Messaging.MessageDescriptor[] AllMessages =");
        builder.AppendLine("        [");

        foreach (var message in state.GetAllMessages())
        {
            builder.Append("            ");
            builder.Append(CreateMessageDescriptorExpression(state, message));
            builder.AppendLine(",");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::LayerZero.Messaging.MessageDescriptor> ByType =");
        builder.AppendLine("            global::System.Linq.Enumerable.ToDictionary(AllMessages, static descriptor => descriptor.MessageType);");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<string, global::LayerZero.Messaging.MessageDescriptor> ByName =");
        builder.AppendLine("            global::System.Linq.Enumerable.ToDictionary(AllMessages, static descriptor => descriptor.Name, global::System.StringComparer.Ordinal);");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Messaging.MessageDescriptor> Messages => AllMessages;");
        builder.AppendLine();
        builder.AppendLine("        public bool TryGetDescriptor(global::System.Type messageType, out global::LayerZero.Messaging.MessageDescriptor descriptor)");
        builder.AppendLine("        {");
        builder.AppendLine("            return ByType.TryGetValue(messageType, out descriptor!);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryGetDescriptor(string messageName, out global::LayerZero.Messaging.MessageDescriptor descriptor)");
        builder.AppendLine("        {");
        builder.AppendLine("            return ByName.TryGetValue(messageName, out descriptor!);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    internal sealed class {MessagingTopologyManifestClassName} : global::LayerZero.Messaging.IMessageTopologyManifest");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly global::LayerZero.Messaging.MessageTopologyDescriptor[] AllMessages =");
        builder.AppendLine("        [");

        foreach (var message in state.GetAllMessages())
        {
            builder.Append("            new global::LayerZero.Messaging.MessageTopologyDescriptor(");
            builder.Append(CreateMessageDescriptorExpression(state, message));

            var handlers = state.GetHandlers(message);
            if (handlers.Count == 0)
            {
                builder.Append("),");
                builder.AppendLine();
                continue;
            }

            builder.AppendLine(",");
            builder.AppendLine("                [");
            foreach (var handler in handlers)
            {
                builder.Append("                    new global::LayerZero.Messaging.MessageSubscriptionDescriptor(");
                builder.Append($"\"{Escape(handler.ImplementationType)}\", ");
                builder.Append($"typeof({handler.ImplementationType}), ");
                builder.Append(handler.RequiresIdempotency ? "true" : "false");
                builder.AppendLine("),");
            }

            builder.AppendLine("                ]),");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::LayerZero.Messaging.MessageTopologyDescriptor> ByType =");
        builder.AppendLine("            global::System.Linq.Enumerable.ToDictionary(AllMessages, static descriptor => descriptor.Message.MessageType);");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<string, global::LayerZero.Messaging.MessageTopologyDescriptor> ByName =");
        builder.AppendLine("            global::System.Linq.Enumerable.ToDictionary(AllMessages, static descriptor => descriptor.Message.Name, global::System.StringComparer.Ordinal);");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Messaging.MessageTopologyDescriptor> Messages => AllMessages;");
        builder.AppendLine();
        builder.AppendLine("        public bool TryGetDescriptor(global::System.Type messageType, out global::LayerZero.Messaging.MessageTopologyDescriptor descriptor)");
        builder.AppendLine("        {");
        builder.AppendLine("            return ByType.TryGetValue(messageType, out descriptor!);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryGetDescriptor(string messageName, out global::LayerZero.Messaging.MessageTopologyDescriptor descriptor)");
        builder.AppendLine("        {");
        builder.AppendLine("            return ByName.TryGetValue(messageName, out descriptor!);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();

        builder.AppendLine($"    internal sealed partial class {MessagingJsonContextClassName} : global::System.Text.Json.Serialization.JsonSerializerContext");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly global::System.Text.Json.JsonSerializerOptions OptionsValue = new(global::System.Text.Json.JsonSerializerDefaults.Web);");
        builder.AppendLine("        private static readonly global::System.Text.Json.JsonSerializerOptions ResolverOptions = new(global::System.Text.Json.JsonSerializerDefaults.Web)");
        builder.AppendLine("        {");
        builder.AppendLine("            TypeInfoResolver = new global::System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine($"        public static {MessagingJsonContextClassName} Default {{ get; }} = new();");
        builder.AppendLine();
        builder.AppendLine($"        public {MessagingJsonContextClassName}()");
        builder.AppendLine("            : base(OptionsValue)");
        builder.AppendLine("        {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine($"        public {MessagingJsonContextClassName}(global::System.Text.Json.JsonSerializerOptions options)");
        builder.AppendLine("            : base(options)");
        builder.AppendLine("        {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        protected override global::System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => OptionsValue;");
        builder.AppendLine();
        builder.AppendLine("        public override global::System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(global::System.Type type)");
        builder.AppendLine("        {");
        builder.AppendLine("            return ResolverOptions.GetTypeInfo(type);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (var invocation in handledInvocations)
        {
            RenderInvoker(builder, state, invocation.Message, invocation.Handler);
            builder.AppendLine();
        }

        builder.AppendLine("}");
        builder.AppendLine("#pragma warning restore CS1591");
        return builder.ToString();
    }

    private static void RenderInvoker(StringBuilder builder, MessageGenerationState state, MessageDefinition message, HandlerDefinition handler)
    {
        var invokerClassName = GetInvokerClassName(message.TypeName, handler.ImplementationType);
        builder.AppendLine($"    internal sealed class {invokerClassName} : global::LayerZero.Messaging.IMessageHandlerInvoker");
        builder.AppendLine("    {");
        builder.AppendLine($"        private static readonly global::LayerZero.Messaging.MessageDescriptor DescriptorValue = {CreateMessageDescriptorExpression(state, message)};");
        builder.AppendLine();
        builder.AppendLine("        public global::LayerZero.Messaging.MessageDescriptor Descriptor => DescriptorValue;");
        builder.AppendLine();
        builder.AppendLine($"        public string HandlerIdentity => \"{Escape(handler.ImplementationType)}\";");
        builder.AppendLine();
        var requiresIdempotency = message.RequiresIdempotency || handler.RequiresIdempotency;
        builder.AppendLine($"        public bool RequiresIdempotency => {(requiresIdempotency ? "true" : "false")};");
        builder.AppendLine();
        builder.AppendLine("        public async global::System.Threading.Tasks.ValueTask<global::LayerZero.Messaging.MessageHandlingResult> InvokeAsync(");
        builder.AppendLine("            global::System.IServiceProvider services,");
        builder.AppendLine("            object message,");
        builder.AppendLine("            global::LayerZero.Messaging.MessageContext context,");
        builder.AppendLine("            global::System.Threading.CancellationToken cancellationToken = default)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(message);");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(context);");
        builder.AppendLine();
        builder.AppendLine($"            var typedMessage = ({message.TypeName})message;");

        foreach (var validatorType in state.ValidatorTypesByMessageType.GetValueOrEmpty(message.TypeName))
        {
            builder.AppendLine();
            builder.AppendLine($"            var {GetLocalName(validatorType)} = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{validatorType}>(services);");
            builder.AppendLine($"            var {GetLocalName(validatorType)}Validation = await {GetLocalName(validatorType)}");
            builder.AppendLine("                .ValidateAsync(");
            builder.AppendLine("                    typedMessage,");
            builder.AppendLine("                    global::LayerZero.Validation.ValidationContext.Empty,");
            builder.AppendLine("                    cancellationToken)");
            builder.AppendLine("                .ConfigureAwait(false);");
            builder.AppendLine($"            if ({GetLocalName(validatorType)}Validation.IsInvalid)");
            builder.AppendLine("            {");
            builder.AppendLine($"                return global::LayerZero.Messaging.MessageHandlingResult.ValidationFailure({GetLocalName(validatorType)}Validation);");
            builder.AppendLine("            }");
        }

        if (requiresIdempotency)
        {
            builder.AppendLine();
            builder.AppendLine("            var idempotencyStore = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::LayerZero.Messaging.IMessageIdempotencyStore>(services);");
        }

        RenderHandlerInvocation(builder, message, handler, message.RequiresIdempotency);

        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void RenderHandlerInvocation(
        StringBuilder builder,
        MessageDefinition message,
        HandlerDefinition handler,
        bool messageRequiresIdempotency)
    {
        var handlerLocal = GetLocalName(handler.ImplementationType);
        var resultLocal = handlerLocal + "Result";
        var dedupeRequired = messageRequiresIdempotency || handler.RequiresIdempotency;
        var dedupeKeyLiteral = Escape($"{handler.ImplementationType}");

        builder.AppendLine($"            var {handlerLocal} = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{handler.ImplementationType}>(services);");

        if (dedupeRequired)
        {
            builder.AppendLine($"            var {handlerLocal}DeduplicationKey = $\"{{context.MessageId}}:{dedupeKeyLiteral}\";");
            builder.AppendLine($"            if (!await idempotencyStore!.TryBeginAsync({handlerLocal}DeduplicationKey, cancellationToken).ConfigureAwait(false))");
            builder.AppendLine("            {");
            builder.AppendLine("                return global::LayerZero.Messaging.MessageHandlingResult.Success();");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            try");
            builder.AppendLine("            {");
        }

        builder.AppendLine($"            var {resultLocal} = await {handlerLocal}");
        builder.AppendLine("                .HandleAsync(typedMessage, cancellationToken)");
        builder.AppendLine("                .ConfigureAwait(false);");
        builder.AppendLine($"            if ({resultLocal}.IsFailure)");
        builder.AppendLine("            {");
        if (dedupeRequired)
        {
            builder.AppendLine($"                await idempotencyStore!.AbandonAsync({handlerLocal}DeduplicationKey, cancellationToken).ConfigureAwait(false);");
        }

        builder.AppendLine($"                return global::LayerZero.Messaging.MessageHandlingResult.FromResult({resultLocal});");
        builder.AppendLine("            }");

        if (dedupeRequired)
        {
            builder.AppendLine();
            builder.AppendLine($"            await idempotencyStore!.CompleteAsync({handlerLocal}DeduplicationKey, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine();
            builder.AppendLine($"            return global::LayerZero.Messaging.MessageHandlingResult.FromResult({resultLocal});");
            builder.AppendLine("            }");
            builder.AppendLine("            catch");
            builder.AppendLine("            {");
            builder.AppendLine($"                await idempotencyStore!.AbandonAsync({handlerLocal}DeduplicationKey, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
        }

        if (!dedupeRequired)
        {
            builder.AppendLine();
            builder.AppendLine($"            return global::LayerZero.Messaging.MessageHandlingResult.FromResult({resultLocal});");
        }
    }

    private static string CreateMessageDescriptorExpression(MessageGenerationState state, MessageDefinition message)
    {
        var builder = new StringBuilder();
        builder.Append("new global::LayerZero.Messaging.MessageDescriptor(");
        builder.Append($"\"{Escape(message.LogicalName)}\", ");
        builder.Append($"typeof({message.TypeName}), ");
        builder.Append(message.Kind == GeneratedMessageKind.Command
            ? "global::LayerZero.Messaging.MessageKind.Command, "
            : "global::LayerZero.Messaging.MessageKind.Event, ");
        builder.Append($"{MessagingJsonContextClassName}.Default.GetTypeInfo(typeof({message.TypeName}))!, ");
        builder.Append($"global::LayerZero.Messaging.MessageTopologyNames.Entity({(message.Kind == GeneratedMessageKind.Command ? "global::LayerZero.Messaging.MessageKind.Command" : "global::LayerZero.Messaging.MessageKind.Event")}, \"{Escape(message.LogicalName)}\"), ");
        builder.Append(state.MessageRequiresIdempotency(message) ? "true" : "false");

        if (message.AffinityMemberName is null)
        {
            builder.Append(", null, null)");
            return builder.ToString();
        }

        builder.Append($", \"{Escape(message.AffinityMemberName)}\", ");
        builder.Append($"static message => global::System.Convert.ToString((({message.TypeName})message).{message.AffinityMemberName}, global::System.Globalization.CultureInfo.InvariantCulture))");
        return builder.ToString();
    }

    private static void AppendRegistration(StringBuilder builder, Registration registration)
    {
        var methodName = registration.Kind == RegistrationKind.TryAdd ? "TryAdd" : "TryAddEnumerable";
        builder.AppendLine();
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions." + methodName + "(");
        builder.AppendLine("                services,");
        builder.AppendLine($"                global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<{registration.ServiceType}, {registration.ImplementationType}>());");
    }

    private static string GetInvokerClassName(string typeName, string handlerTypeName)
    {
        var sanitized = new StringBuilder(typeName.Length + handlerTypeName.Length + 8);
        foreach (var character in $"{typeName}_{handlerTypeName}")
        {
            sanitized.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return sanitized + "_MessageInvoker";
    }

    private static string GetLocalName(string typeName)
    {
        var builder = new StringBuilder();
        foreach (var character in typeName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        if (builder.Length == 0)
        {
            builder.Append("value");
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string CreateGeneratedTypeName(string prefix, string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return $"{prefix}_Assembly";
        }

        var value = assemblyName!;
        var builder = new StringBuilder(value.Length + prefix.Length + 1);
        builder.Append(prefix);
        builder.Append('_');

        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.ToString();
    }

    private readonly struct Registration : IEquatable<Registration>
    {
        public Registration(string serviceType, string implementationType, RegistrationKind kind)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Kind = kind;
        }

        public string ServiceType { get; }

        public string ImplementationType { get; }

        public RegistrationKind Kind { get; }

        public bool Equals(Registration other)
        {
            return ServiceType.Equals(other.ServiceType, StringComparison.Ordinal)
                && ImplementationType.Equals(other.ImplementationType, StringComparison.Ordinal)
                && Kind == other.Kind;
        }

        public override bool Equals(object? obj) => obj is Registration other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ServiceType);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ImplementationType);
                hash = (hash * 31) + Kind.GetHashCode();
                return hash;
            }
        }
    }

    private readonly struct MessageDefinition
    {
        public MessageDefinition(
            string typeName,
            string logicalName,
            GeneratedMessageKind kind,
            Location? location,
            bool requiresIdempotency,
            string? affinityMemberName)
        {
            TypeName = typeName;
            LogicalName = logicalName;
            Kind = kind;
            Location = location;
            RequiresIdempotency = requiresIdempotency;
            AffinityMemberName = affinityMemberName;
        }

        public string TypeName { get; }

        public string LogicalName { get; }

        public GeneratedMessageKind Kind { get; }

        public Location? Location { get; }

        public bool RequiresIdempotency { get; }

        public string? AffinityMemberName { get; }
    }

    private readonly struct HandlerDefinition
    {
        public HandlerDefinition(string implementationType, bool requiresIdempotency)
        {
            ImplementationType = implementationType;
            RequiresIdempotency = requiresIdempotency;
        }

        public string ImplementationType { get; }

        public bool RequiresIdempotency { get; }
    }

    private sealed class MessageGenerationState
    {
        public Dictionary<string, MessageDefinition> MessagesByType { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<string>> ValidatorTypesByMessageType { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<HandlerDefinition>> CommandHandlerTypesByMessageType { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<HandlerDefinition>> EventHandlerTypesByMessageType { get; } = new(StringComparer.Ordinal);

        public IEnumerable<MessageDefinition> GetAllMessages()
        {
            return MessagesByType.Values.OrderBy(static message => message.LogicalName, StringComparer.Ordinal);
        }

        public IEnumerable<MessageDefinition> GetHandledMessages()
        {
            return MessagesByType.Values
                .Where(message =>
                    (CommandHandlerTypesByMessageType.TryGetValue(message.TypeName, out var commandHandlers) && commandHandlers.Count == 1)
                    || (EventHandlerTypesByMessageType.TryGetValue(message.TypeName, out var eventHandlers) && eventHandlers.Count > 0))
                .OrderBy(static message => message.LogicalName, StringComparer.Ordinal);
        }

        public IReadOnlyList<HandlerDefinition> GetHandlers(MessageDefinition message)
        {
            return message.Kind == GeneratedMessageKind.Command
                ? CommandHandlerTypesByMessageType.GetValueOrEmpty(message.TypeName)
                : EventHandlerTypesByMessageType.GetValueOrEmpty(message.TypeName);
        }

        public IEnumerable<(MessageDefinition Message, HandlerDefinition Handler)> GetHandledInvocations()
        {
            foreach (var message in MessagesByType.Values.OrderBy(static entry => entry.LogicalName, StringComparer.Ordinal))
            {
                foreach (var handler in GetHandlers(message).OrderBy(static entry => entry.ImplementationType, StringComparer.Ordinal))
                {
                    yield return (message, handler);
                }
            }
        }

        public bool MessageRequiresIdempotency(MessageDefinition message)
        {
            return message.RequiresIdempotency || GetHandlers(message).Any(static handler => handler.RequiresIdempotency);
        }

        public IEnumerable<(Location? LeftLocation, string LeftType, string RightType, string MessageName)> FindDuplicateMessageNames()
        {
            return MessagesByType.Values
                .GroupBy(static message => message.LogicalName, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .SelectMany(static group =>
                {
                    var messages = group.OrderBy(static message => message.TypeName, StringComparer.Ordinal).ToArray();
                    var duplicates = new List<(Location?, string, string, string)>();
                    for (var index = 1; index < messages.Length; index++)
                    {
                        duplicates.Add((messages[0].Location, messages[0].TypeName, messages[index].TypeName, group.Key));
                    }

                    return duplicates;
                });
        }

        public IEnumerable<(Location? Location, string MessageType, IReadOnlyList<string> HandlerTypes)> FindDuplicateCommandHandlers()
        {
            foreach (var pair in CommandHandlerTypesByMessageType)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                yield return (
                    MessagesByType.TryGetValue(pair.Key, out var message) ? message.Location : null,
                    pair.Key,
                    pair.Value.Select(static handler => handler.ImplementationType).OrderBy(static value => value, StringComparer.Ordinal).ToArray());
            }
        }
    }

    private readonly struct InvalidAffinityMemberDefinition
    {
        public InvalidAffinityMemberDefinition(string memberName, Location? location)
        {
            MemberName = memberName;
            Location = location;
        }

        public string MemberName { get; }

        public Location? Location { get; }
    }

    private enum RegistrationKind
    {
        TryAdd,
        TryAddEnumerable,
    }

    private enum GeneratedMessageKind
    {
        Command = 0,
        Event = 1,
    }
}

internal static class DictionaryExtensions
{
    public static List<TValue> GetOrAdd<TValue>(
        this Dictionary<string, List<TValue>> source,
        string key)
    {
        if (source.TryGetValue(key, out var values))
        {
            return values;
        }

        values = [];
        source[key] = values;
        return values;
    }

    public static IReadOnlyList<TValue> GetValueOrEmpty<TValue>(
        this Dictionary<string, List<TValue>> source,
        string key)
    {
        return source.TryGetValue(key, out var values) ? values : [];
    }
}
