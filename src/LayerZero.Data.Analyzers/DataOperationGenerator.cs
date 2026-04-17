using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Data.Analyzers;

/// <summary>
/// Generates LayerZero data registrations for entity maps and reusable operations.
/// </summary>
[Generator]
public sealed class DataOperationGenerator : IIncrementalGenerator
{
    private const string ExtensionsNamespace = "LayerZero.Data";
    private const string ExtensionsClassName = "LayerZeroGeneratedDataExtensions";

    private static readonly DiagnosticDescriptor ExtensionCollisionRule = new(
        id: "LZDATA001",
        title: "Generated data extension collision",
        messageFormat: "Type '{0}.{1}' already exists and would collide with generated LayerZero data extensions",
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

    private static readonly DiagnosticDescriptor InvalidQueryHandlerRule = new(
        id: "LZDATA003",
        title: "Invalid data query handler contract",
        messageFormat: "Query handler '{0}' must use a query type that implements IDataQuery<{1}>",
        category: "LayerZero.Data",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidMutationHandlerRule = new(
        id: "LZDATA004",
        title: "Invalid data mutation handler contract",
        messageFormat: "Mutation handler '{0}' must use a mutation type that implements IDataMutation<{1}>",
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

            if (HasGeneratedExtensionCollision(compilation))
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    ExtensionCollisionRule,
                    Location.None,
                    ExtensionsNamespace,
                    ExtensionsClassName));
                return;
            }

            var maps = symbols
                .Select(TryCreateMapRegistration)
                .Where(static registration => registration is not null)
                .Cast<MapRegistration>()
                .ToArray();

            foreach (var duplicate in maps.GroupBy(static map => map.EntityType, SymbolEqualityComparer.Default).Where(static group => group.Count() > 1))
            {
                var entityType = duplicate.Key;
                if (entityType is null)
                {
                    continue;
                }

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
                    if (handler.Kind == HandlerKind.Query && !ImplementsQueryContract(handler.RequestType, handler.ResultType))
                    {
                        productionContext.ReportDiagnostic(Diagnostic.Create(
                            InvalidQueryHandlerRule,
                            handler.Location,
                            handler.ImplementationType,
                            handler.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                        continue;
                    }

                    if (handler.Kind == HandlerKind.Mutation && !ImplementsMutationContract(handler.RequestType, handler.ResultType))
                    {
                        productionContext.ReportDiagnostic(Diagnostic.Create(
                            InvalidMutationHandlerRule,
                            handler.Location,
                            handler.ImplementationType,
                            handler.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                        continue;
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

            productionContext.AddSource(
                "LayerZero.Data.Extensions.g.cs",
                SourceText.From(RenderSource(maps, queryHandlers, mutationHandlers), Encoding.UTF8));
        });
    }

    private static string RenderSource(
        IReadOnlyList<MapRegistration> maps,
        IReadOnlyList<HandlerRegistration> queryHandlers,
        IReadOnlyList<HandlerRegistration> mutationHandlers)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {ExtensionsNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    public static partial class LayerZeroGeneratedDataExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddDataOperations(");
        builder.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");

        foreach (var map in maps.OrderBy(static map => map.MapType, StringComparer.Ordinal))
        {
            builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(");
            builder.AppendLine("                services,");
            builder.AppendLine($"                global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::LayerZero.Data.IEntityMap, {map.MapType}>());");
        }

        foreach (var handler in queryHandlers.OrderBy(static handler => handler.ImplementationType, StringComparer.Ordinal))
        {
            builder.AppendLine($"            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddTransient<{handler.InterfaceType}, {handler.ImplementationType}>(services);");
        }

        foreach (var handler in mutationHandlers.OrderBy(static handler => handler.ImplementationType, StringComparer.Ordinal))
        {
            builder.AppendLine($"            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddTransient<{handler.InterfaceType}, {handler.ImplementationType}>(services);");
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static MapRegistration? TryCreateMapRegistration(ITypeSymbol symbol)
    {
        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
        {
            return null;
        }

        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (IsType(current, "LayerZero.Data", "EntityMap", arity: 1))
            {
                return new MapRegistration(
                    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    current.TypeArguments[0],
                    symbol.Locations.FirstOrDefault() ?? Location.None);
            }
        }

        return null;
    }

    private static IEnumerable<HandlerRegistration> GetHandlerRegistrations(ITypeSymbol symbol)
    {
        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
        {
            yield break;
        }

        foreach (var interfaceType in symbol.AllInterfaces)
        {
            if (IsType(interfaceType, "LayerZero.Data", "IDataQueryHandler", arity: 2))
            {
                yield return new HandlerRegistration(
                    HandlerKind.Query,
                    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.TypeArguments[0],
                    interfaceType.TypeArguments[1],
                    symbol.Locations.FirstOrDefault() ?? Location.None);
            }
            else if (IsType(interfaceType, "LayerZero.Data", "IDataMutationHandler", arity: 2))
            {
                yield return new HandlerRegistration(
                    HandlerKind.Mutation,
                    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    interfaceType.TypeArguments[0],
                    interfaceType.TypeArguments[1],
                    symbol.Locations.FirstOrDefault() ?? Location.None);
            }
        }
    }

    private static bool HasGeneratedExtensionCollision(Compilation compilation)
    {
        var current = compilation.GlobalNamespace.GetNamespaceMembers().FirstOrDefault(static member => member.Name.Equals("LayerZero", StringComparison.Ordinal));
        current = current?.GetNamespaceMembers().FirstOrDefault(static member => member.Name.Equals("Data", StringComparison.Ordinal));
        return current?.GetTypeMembers(ExtensionsClassName).Any() == true;
    }

    private static bool ImplementsQueryContract(ITypeSymbol requestType, ITypeSymbol resultType)
    {
        return requestType.AllInterfaces.Any(interfaceType =>
            IsType(interfaceType, "LayerZero.Data", "IDataQuery", arity: 1)
            && SymbolEqualityComparer.Default.Equals(interfaceType.TypeArguments[0], resultType));
    }

    private static bool ImplementsMutationContract(ITypeSymbol requestType, ITypeSymbol resultType)
    {
        return requestType.AllInterfaces.Any(interfaceType =>
            IsType(interfaceType, "LayerZero.Data", "IDataMutation", arity: 1)
            && SymbolEqualityComparer.Default.Equals(interfaceType.TypeArguments[0], resultType));
    }

    private static bool IsType(INamedTypeSymbol symbol, string @namespace, string name, int arity) =>
        symbol.ContainingNamespace.ToDisplayString().Equals(@namespace, StringComparison.Ordinal)
        && symbol.Name.Equals(name, StringComparison.Ordinal)
        && symbol.Arity == arity;

    private sealed record MapRegistration(string MapType, ITypeSymbol EntityType, Location Location);

    private sealed record HandlerRegistration(
        HandlerKind Kind,
        string ImplementationType,
        string InterfaceType,
        ITypeSymbol RequestType,
        ITypeSymbol ResultType,
        Location Location);

    private enum HandlerKind
    {
        Query = 0,
        Mutation = 1,
    }
}
