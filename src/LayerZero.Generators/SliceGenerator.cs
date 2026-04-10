using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Generators;

/// <summary>
/// Generates slice registration and endpoint mapping extensions.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SliceGenerator : IIncrementalGenerator
{
    private const string GeneratedClassName = "LayerZeroGeneratedSliceExtensions";
    private const string GeneratedNamespace = "LayerZero.AspNetCore";
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
        "The type '{0}.{1}' already exists; rename it so LayerZero can generate AddSlices and MapSlices",
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

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => GetClassSymbol(syntaxContext))
            .Where(static symbol => symbol is not null);

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol?> CandidateTypes)> source =
            context.CompilationProvider.Combine(candidateTypes.Collect());

        context.RegisterSourceOutput(source, static (context, source) =>
        {
            Execute(context, source.Compilation, source.CandidateTypes);
        });
    }

    private static INamedTypeSymbol? GetClassSymbol(GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> candidateTypes)
    {
        if (HasGeneratedExtensionCollision(compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ExtensionCollision,
                Location.None,
                GeneratedNamespace,
                GeneratedClassName));
            return;
        }

        var endpointSlices = new List<string>();
        var registrations = new List<Registration>();

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

            if (discoveredRegistrations.Count == 0)
            {
                continue;
            }

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

        var source = RenderSource(
            endpointSlices.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal),
            registrations.Distinct().OrderBy(registration => registration.ServiceType, StringComparer.Ordinal)
                .ThenBy(registration => registration.ImplementationType, StringComparer.Ordinal)
                .ThenBy(registration => registration.Kind));

        context.AddSource("LayerZero.Slices.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static List<Registration> DiscoverRegistrations(INamedTypeSymbol symbol)
    {
        var registrations = new List<Registration>();
        var implementationType = symbol.ToDisplayString(FullyQualifiedFormat);

        foreach (var interfaceType in symbol.AllInterfaces)
        {
            if (IsInterface(interfaceType, "LayerZero.Validation", "IValidator", 1))
            {
                registrations.Add(new Registration(
                    interfaceType.ToDisplayString(FullyQualifiedFormat),
                    implementationType,
                    RegistrationKind.TryAddEnumerable));
                continue;
            }

            if (IsInterface(interfaceType, "LayerZero.Core", "IEventHandler", 1))
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
        return symbol.TypeKind == TypeKind.Class
            && !symbol.IsStatic
            && !symbol.IsAbstract
            && !symbol.IsGenericType
            && !symbol.IsUnboundGenericType;
    }

    private static bool HasMapEndpointCandidate(INamedTypeSymbol symbol)
    {
        return symbol
            .GetMembers("MapEndpoint")
            .OfType<IMethodSymbol>()
            .Any();
    }

    private static bool IsValidHttpSliceModule(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind == TypeKind.Class
            && symbol.IsStatic
            && !symbol.IsGenericType
            && !symbol.IsUnboundGenericType
            && symbol
            .GetMembers("MapEndpoint")
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
            .Select(reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static bool IsEndpointRouteBuilder(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            && namedType.ContainingNamespace.ToDisplayString().Equals("Microsoft.AspNetCore.Routing", StringComparison.Ordinal)
            && namedType.Name.Equals("IEndpointRouteBuilder", StringComparison.Ordinal);
    }

    private static bool HasGeneratedExtensionCollision(Compilation compilation)
    {
        var layerZeroNamespace = compilation.GlobalNamespace
            .GetNamespaceMembers()
            .FirstOrDefault(namespaceSymbol => namespaceSymbol.Name.Equals("LayerZero", StringComparison.Ordinal));

        var aspNetCoreNamespace = layerZeroNamespace?
            .GetNamespaceMembers()
            .FirstOrDefault(namespaceSymbol => namespaceSymbol.Name.Equals("AspNetCore", StringComparison.Ordinal));

        return aspNetCoreNamespace?
            .GetTypeMembers(GeneratedClassName)
            .Any() == true;
    }

    private static string RenderSource(IEnumerable<string> endpointSlices, IEnumerable<Registration> registrations)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {GeneratedNamespace}");
        builder.AppendLine("{");
        builder.AppendLine($"    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"LayerZero.Generators\", \"0.1.0-alpha.1\")]");
        builder.AppendLine($"    public static partial class {GeneratedClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddSlices(");
        builder.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");

        foreach (var registration in registrations)
        {
            var methodName = registration.Kind == RegistrationKind.TryAdd ? "TryAdd" : "TryAddEnumerable";
            builder.AppendLine();
            builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions." + methodName + "(");
            builder.AppendLine("                services,");
            builder.AppendLine($"                global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<{registration.ServiceType}, {registration.ImplementationType}>());");
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
        builder.AppendLine("}");

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

    private enum RegistrationKind
    {
        TryAdd,
        TryAddEnumerable,
    }
}
