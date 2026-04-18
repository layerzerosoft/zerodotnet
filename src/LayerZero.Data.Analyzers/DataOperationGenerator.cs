using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Data.Analyzers;

/// <summary>
/// Generates LayerZero data registrations for entity maps and reusable operations.
/// </summary>
[Generator]
public sealed class DataOperationGenerator : IIncrementalGenerator
{
    private const string GeneratedNamespace = "LayerZero.Data.Generated";

    private static readonly DiagnosticDescriptor GeneratedTypeCollisionRule = new(
        id: "LZDATA001",
        title: "Generated data registrar collision",
        messageFormat: "Type '{0}' already exists and would collide with generated LayerZero data registration infrastructure",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMapRule = new(
        id: "LZDATA002",
        title: "Duplicate entity maps are not supported",
        messageFormat: "Entity type '{0}' has multiple LayerZero data maps",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateQueryHandlerRule = new(
        id: "LZDATA003",
        title: "Duplicate data query handlers are not supported",
        messageFormat: "Query contract '{0}' has multiple LayerZero data handlers",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMutationHandlerRule = new(
        id: "LZDATA004",
        title: "Duplicate data mutation handlers are not supported",
        messageFormat: "Mutation contract '{0}' has multiple LayerZero data handlers",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonInstantiableRegistrationRule = new(
        id: "LZDATA005",
        title: "Auto-registered data types must be instantiable",
        messageFormat: "Type '{0}' cannot be auto-registered because it is not an instantiable class",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, _) => (ITypeSymbol?)syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node))
            .Where(static symbol => symbol is not null)
            .Collect();

        var compilationAndDeclarations = context.CompilationProvider.Combine(declarations);
        context.RegisterSourceOutput(compilationAndDeclarations, static (productionContext, source) =>
        {
            var compilation = source.Left;
            var symbols = source.Right
                .Where(static symbol => symbol is not null)
                .Cast<ITypeSymbol>()
                .ToArray();
            var hasErrors = false;

            var generatedNames = GetGeneratedNames(compilation.AssemblyName ?? "LayerZero.Data.Assembly");
            foreach (var collision in FindGeneratedTypeCollisions(compilation, generatedNames))
            {
                hasErrors = true;
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    GeneratedTypeCollisionRule,
                    Location.None,
                    collision));
            }

            var maps = symbols
                .Select(TryCreateMapRegistration)
                .Where(static registration => registration is not null)
                .Cast<MapRegistration>()
                .ToArray();

            foreach (var map in maps)
            {
                if (!IsInstantiable(map.Symbol))
                {
                    hasErrors = true;
                    productionContext.ReportDiagnostic(Diagnostic.Create(
                        NonInstantiableRegistrationRule,
                        map.Location,
                        map.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }

            foreach (var duplicate in maps.GroupBy(static map => map.EntityType, SymbolEqualityComparer.Default).Where(static group => group.Count() > 1))
            {
                var entityType = duplicate.Key;
                if (entityType is null)
                {
                    continue;
                }

                hasErrors = true;
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMapRule,
                    duplicate.First().Location,
                    entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }

            var queryHandlers = new List<HandlerRegistration>();
            var mutationHandlers = new List<HandlerRegistration>();
            foreach (var symbol in symbols)
            {
                foreach (var handler in GetHandlerRegistrations(symbol))
                {
                    if (!IsInstantiable(handler.Symbol))
                    {
                        hasErrors = true;
                        productionContext.ReportDiagnostic(Diagnostic.Create(
                            NonInstantiableRegistrationRule,
                            handler.Location,
                            handler.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    }

                    if (handler.Kind == HandlerKind.Query)
                    {
                        queryHandlers.Add(handler);
                    }
                    else
                    {
                        mutationHandlers.Add(handler);
                    }
                }
            }

            foreach (var duplicate in queryHandlers
                .GroupBy(static handler => handler.InterfaceType, SymbolEqualityComparer.Default)
                .Where(static group => group.Count() > 1))
            {
                if (duplicate.Key is null)
                {
                    continue;
                }

                hasErrors = true;
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateQueryHandlerRule,
                    duplicate.First().Location,
                    duplicate.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }

            foreach (var duplicate in mutationHandlers
                .GroupBy(static handler => handler.InterfaceType, SymbolEqualityComparer.Default)
                .Where(static group => group.Count() > 1))
            {
                if (duplicate.Key is null)
                {
                    continue;
                }

                hasErrors = true;
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMutationHandlerRule,
                    duplicate.First().Location,
                    duplicate.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }

            if (hasErrors)
            {
                return;
            }

            var referencedRegistrars = GetReferencedRegistrarTypes(compilation)
                .Append($"{GeneratedNamespace}.{generatedNames.RegistrarTypeName}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();

            productionContext.AddSource(
                "LayerZero.Data.Registrations.g.cs",
                SourceText.From(
                    RenderSource(
                        generatedNames,
                        maps,
                        queryHandlers,
                        mutationHandlers,
                        referencedRegistrars),
                    Encoding.UTF8));
        });
    }

    private static string RenderSource(
        GeneratedTypeNames generatedNames,
        IReadOnlyList<MapRegistration> maps,
        IReadOnlyList<HandlerRegistration> queryHandlers,
        IReadOnlyList<HandlerRegistration> mutationHandlers,
        IReadOnlyList<string> registrarTypes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine();
        builder.AppendLine($"[assembly: global::LayerZero.Data.DataAssemblyRegistrarAttribute(typeof(global::{GeneratedNamespace}.{generatedNames.RegistrarTypeName}))]");
        builder.AppendLine();
        builder.AppendLine($"namespace {GeneratedNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers LayerZero data services generated for this assembly.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"    public sealed class {generatedNames.RegistrarTypeName} : global::LayerZero.Data.IDataAssemblyRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers generated LayerZero data services.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        /// <param name=\"builder\">The generated registration builder.</param>");
        builder.AppendLine("        public void Register(global::LayerZero.Data.DataAssemblyRegistrationBuilder builder)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(builder);");

        foreach (var map in maps.OrderBy(static map => map.MapType, StringComparer.Ordinal))
        {
            builder.AppendLine($"            builder.AddEntityMap<{map.MapType}>();");
        }

        foreach (var handler in queryHandlers.OrderBy(static handler => handler.ImplementationType, StringComparer.Ordinal))
        {
            builder.AppendLine($"            builder.AddQueryHandler<{handler.ImplementationType}, {handler.RequestType}, {handler.ResultType}>();");
        }

        foreach (var handler in mutationHandlers.OrderBy(static handler => handler.ImplementationType, StringComparer.Ordinal))
        {
            builder.AppendLine($"            builder.AddMutationHandler<{handler.ImplementationType}, {handler.RequestType}, {handler.ResultType}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"    internal static class {generatedNames.ModuleInitializerTypeName}");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Initialize()");
        builder.AppendLine("        {");

        foreach (var registrarType in registrarTypes)
        {
            builder.AppendLine($"            global::LayerZero.Data.DataAssemblyRegistrarCatalog.Register<{registrarType}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("#pragma warning restore CS1591");
        return builder.ToString();
    }

    private static IEnumerable<string> FindGeneratedTypeCollisions(Compilation compilation, GeneratedTypeNames generatedNames)
    {
        var current = compilation.GlobalNamespace
            .GetNamespaceMembers()
            .FirstOrDefault(static member => member.Name.Equals("LayerZero", StringComparison.Ordinal));
        current = current?.GetNamespaceMembers().FirstOrDefault(static member => member.Name.Equals("Data", StringComparison.Ordinal));
        current = current?.GetNamespaceMembers().FirstOrDefault(static member => member.Name.Equals("Generated", StringComparison.Ordinal));

        if (current is null)
        {
            yield break;
        }

        foreach (var typeName in new[] { generatedNames.RegistrarTypeName, generatedNames.ModuleInitializerTypeName })
        {
            if (current.GetTypeMembers(typeName).Any())
            {
                yield return $"{GeneratedNamespace}.{typeName}";
            }
        }
    }

    private static IEnumerable<string> GetReferencedRegistrarTypes(Compilation compilation)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass is not INamedTypeSymbol attributeType
                    || !IsType(attributeType, "LayerZero.Data", "DataAssemblyRegistrarAttribute", arity: 0)
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

    private static MapRegistration? TryCreateMapRegistration(ITypeSymbol symbol)
    {
        if (symbol is not INamedTypeSymbol namedType
            || symbol.IsAbstract
            || symbol.TypeKind == TypeKind.Interface)
        {
            return null;
        }

        for (var current = namedType.BaseType; current is not null; current = current.BaseType)
        {
            if (IsType(current, "LayerZero.Data", "EntityMap", arity: 1))
            {
                return new MapRegistration(
                    namedType,
                    namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    current.TypeArguments[0],
                    namedType.Locations.FirstOrDefault() ?? Location.None);
            }
        }

        return null;
    }

    private static IEnumerable<HandlerRegistration> GetHandlerRegistrations(ITypeSymbol symbol)
    {
        if (symbol is not INamedTypeSymbol namedType
            || symbol.IsAbstract
            || symbol.TypeKind == TypeKind.Interface)
        {
            yield break;
        }

        foreach (var interfaceType in namedType.AllInterfaces)
        {
            if (IsType(interfaceType, "LayerZero.Data", "IDataQueryHandler", arity: 2))
            {
                yield return new HandlerRegistration(
                    HandlerKind.Query,
                    namedType,
                    namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType,
                    interfaceType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    namedType.Locations.FirstOrDefault() ?? Location.None);
            }
            else if (IsType(interfaceType, "LayerZero.Data", "IDataMutationHandler", arity: 2))
            {
                yield return new HandlerRegistration(
                    HandlerKind.Mutation,
                    namedType,
                    namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType,
                    interfaceType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    namedType.Locations.FirstOrDefault() ?? Location.None);
            }
        }
    }

    private static bool IsInstantiable(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract
            || symbol.IsStatic
            || symbol.IsGenericType
            || symbol.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        return symbol.InstanceConstructors.Any(static constructor =>
            constructor.MethodKind == MethodKind.Constructor
            && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
            || symbol.InstanceConstructors.Length == 0;
    }

    private static GeneratedTypeNames GetGeneratedNames(string assemblyName)
    {
        var safeName = CreateIdentifier(assemblyName);
        return new GeneratedTypeNames(
            $"{safeName}DataAssemblyRegistrar",
            $"{safeName}DataAssemblyModuleInitializer");
    }

    private static string CreateIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length + 1);
        if (value.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(value[0]))
        {
            builder.Append('_');
        }

        foreach (var character in value)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static bool IsType(INamedTypeSymbol symbol, string @namespace, string name, int arity) =>
        symbol.ContainingNamespace.ToDisplayString().Equals(@namespace, StringComparison.Ordinal)
        && symbol.Name.Equals(name, StringComparison.Ordinal)
        && symbol.Arity == arity;

    private sealed record GeneratedTypeNames(string RegistrarTypeName, string ModuleInitializerTypeName);

    private sealed record MapRegistration(
        INamedTypeSymbol Symbol,
        string MapType,
        ITypeSymbol EntityType,
        Location Location);

    private sealed record HandlerRegistration(
        HandlerKind Kind,
        INamedTypeSymbol Symbol,
        string ImplementationType,
        INamedTypeSymbol InterfaceType,
        string RequestType,
        string ResultType,
        Location Location);

    private enum HandlerKind
    {
        Query = 0,
        Mutation = 1,
    }
}
