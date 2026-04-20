using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Migrations.Analyzers;

/// <summary>
/// Generates the internal LayerZero migration catalog.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MigrationCatalogGenerator : IIncrementalGenerator
{
    private const string GeneratedNamespace = "LayerZero.Migrations.Generated";
    private const string CatalogClassNamePrefix = "LayerZeroGeneratedMigrationCatalog";
    private const string RegistrarClassNamePrefix = "LayerZeroGeneratedMigrationRegistrar";
    private const string ModuleInitializerClassNamePrefix = "LayerZeroGeneratedMigrationModuleInitializer";
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly DiagnosticDescriptor UnsupportedConvention = new(
        "LZMIG001",
        "Unsupported migration artifact convention",
        "Migration artifact '{0}' must be a non-generic concrete class with an accessible parameterless constructor and a supported file/folder convention",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMigrationId = new(
        "LZMIG002",
        "Duplicate migration ids are not supported",
        "Migrations '{0}' and '{1}' resolve to the same id '{2}'",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateSeedId = new(
        "LZMIG003",
        "Duplicate seed ids in one profile are not supported",
        "Seeds '{0}' and '{1}' resolve to the same profile/id '{2}/{3}'",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSeedProfile = new(
        "LZMIG004",
        "Invalid seed profile convention",
        "Seed '{0}' uses profile '{1}', but profiles must use lowercase letters, numbers, or dashes",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidArtifactId = new(
        "LZMIG005",
        "Invalid migration artifact id",
        "Migration artifact '{0}' uses id '{1}', but ids must be 14-digit UTC timestamps like yyyyMMddHHmmss",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TypeFileMismatch = new(
        "LZMIG006",
        "Migration type and file name conventions do not match",
        "Migration artifact '{0}' does not match file convention '{1}'",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        var source = context.CompilationProvider.Combine(candidateTypes.Collect());
        context.RegisterSourceOutput(source, static (sourceContext, value) =>
        {
            Execute(sourceContext, value.Left, value.Right);
        });
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> candidateTypes)
    {
        if (compilation.GetTypeByMetadataName("LayerZero.Migrations.IMigrationCatalog") is null)
        {
            return;
        }

        var migrations = new List<ArtifactMetadata>();
        var seeds = new List<ArtifactMetadata>();

        foreach (var symbol in candidateTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            if (IsDerivedFrom(symbol, "LayerZero.Migrations", "Migration"))
            {
                if (!TryCreateMetadata(symbol, isSeed: false, out var metadata, out var mismatchValue))
                {
                    if (mismatchValue is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            UnsupportedConvention,
                            symbol.Locations.FirstOrDefault(),
                            symbol.ToDisplayString()));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            TypeFileMismatch,
                            symbol.Locations.FirstOrDefault(),
                            symbol.ToDisplayString(),
                            mismatchValue));
                    }

                    continue;
                }

                if (!IsValidId(metadata.Id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidArtifactId,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString(),
                        metadata.Id));
                    continue;
                }

                migrations.Add(metadata);
                continue;
            }

            if (IsDerivedFrom(symbol, "LayerZero.Migrations", "Seed"))
            {
                if (!TryCreateMetadata(symbol, isSeed: true, out var metadata, out var mismatchValue))
                {
                    if (mismatchValue is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            UnsupportedConvention,
                            symbol.Locations.FirstOrDefault(),
                            symbol.ToDisplayString()));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            TypeFileMismatch,
                            symbol.Locations.FirstOrDefault(),
                            symbol.ToDisplayString(),
                            mismatchValue));
                    }

                    continue;
                }

                if (!IsValidId(metadata.Id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidArtifactId,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString(),
                        metadata.Id));
                    continue;
                }

                if (!IsValidProfile(metadata.Profile))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidSeedProfile,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString(),
                        metadata.Profile));
                    continue;
                }

                seeds.Add(metadata);
            }
        }

        foreach (var duplicate in migrations
                     .GroupBy(static metadata => metadata.Id, StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1))
        {
            var entries = duplicate.ToArray();
            for (var index = 1; index < entries.Length; index++)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMigrationId,
                    entries[index].Location,
                    entries[0].TypeName,
                    entries[index].TypeName,
                    duplicate.Key));
            }
        }

        foreach (var duplicate in seeds
                     .GroupBy(static metadata => $"{metadata.Profile}|{metadata.Id}", StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1))
        {
            var entries = duplicate.ToArray();
            for (var index = 1; index < entries.Length; index++)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateSeedId,
                    entries[index].Location,
                    entries[0].TypeName,
                    entries[index].TypeName,
                    entries[index].Profile,
                    entries[index].Id));
            }
        }

        var catalogClassName = CreateGeneratedTypeName(CatalogClassNamePrefix, compilation.AssemblyName);
        var registrarClassName = CreateGeneratedTypeName(RegistrarClassNamePrefix, compilation.AssemblyName);
        var moduleInitializerClassName = CreateGeneratedTypeName(ModuleInitializerClassNamePrefix, compilation.AssemblyName);
        var referencedRegistrars = GetReferencedRegistrarTypes(compilation)
            .Append($"global::{GeneratedNamespace}.{registrarClassName}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var referencedProviderRegistrars = GetReferencedProviderRegistrarTypes(compilation)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        context.AddSource(
            "LayerZero.Migrations.Catalog.g.cs",
            SourceText.From(
                RenderSource(
                    migrations,
                    seeds,
                    referencedRegistrars,
                    referencedProviderRegistrars,
                    catalogClassName,
                    registrarClassName,
                    moduleInitializerClassName),
                Encoding.UTF8));
    }

    private static bool TryCreateMetadata(
        INamedTypeSymbol symbol,
        bool isSeed,
        out ArtifactMetadata metadata,
        out string? mismatchValue)
    {
        metadata = default;
        mismatchValue = null;

        if (!IsConcrete(symbol) || !HasAccessibleParameterlessConstructor(symbol))
        {
            return false;
        }

        var path = symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var sourcePath = path!;
        var normalizedPath = sourcePath.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        var underscoreIndex = fileName.IndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex == fileName.Length - 1)
        {
            return false;
        }

        var id = fileName.Substring(0, underscoreIndex);
        var conventionName = fileName.Substring(underscoreIndex + 1);
        var inferredBaseName = StripArtifactSuffix(symbol.Name, isSeed ? "Seed" : "Migration");

        if (!string.Equals(conventionName, inferredBaseName, StringComparison.Ordinal))
        {
            mismatchValue = conventionName;
            return false;
        }

        if (isSeed)
        {
            var marker = "/Seeds/";
            var markerIndex = normalizedPath.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var profileStart = markerIndex + marker.Length;
            var profileEnd = normalizedPath.IndexOf('/', profileStart);
            if (profileEnd < 0)
            {
                return false;
            }

            var profile = normalizedPath.Substring(profileStart, profileEnd - profileStart);
            metadata = new ArtifactMetadata(
                symbol.ToDisplayString(FullyQualifiedFormat),
                id,
                HumanizeName(inferredBaseName),
                profile,
                symbol.Locations.FirstOrDefault());
            return true;
        }

        if (!normalizedPath.Contains("/Migrations/", StringComparison.Ordinal))
        {
            return false;
        }

        metadata = new ArtifactMetadata(
            symbol.ToDisplayString(FullyQualifiedFormat),
            id,
            HumanizeName(inferredBaseName),
            string.Empty,
            symbol.Locations.FirstOrDefault());
        return true;
    }

    private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol symbol)
    {
        return symbol.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0
            && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
    }

    private static bool IsDerivedFrom(INamedTypeSymbol symbol, string @namespace, string name)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ContainingNamespace.ToDisplayString().Equals(@namespace, StringComparison.Ordinal)
                && current.Name.Equals(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConcrete(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind == TypeKind.Class
            && !symbol.IsStatic
            && !symbol.IsAbstract
            && !symbol.IsGenericType
            && !symbol.IsUnboundGenericType;
    }

    private static bool IsValidId(string id)
    {
        return id.Length == 14 && id.All(static character => character is >= '0' and <= '9');
    }

    private static bool IsValidProfile(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile) || profile[0] is < 'a' or > 'z')
        {
            return false;
        }

        return profile.All(static character =>
            character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character == '-');
    }

    private static string StripArtifactSuffix(string name, string suffix)
    {
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - suffix.Length)
            : name;
    }

    private static string HumanizeName(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> GetReferencedRegistrarTypes(Compilation compilation)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "LayerZero.Migrations"
                    || !attribute.AttributeClass.Name.Equals("MigrationAssemblyRegistrarAttribute", StringComparison.Ordinal)
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

    private static IEnumerable<string> GetReferencedProviderRegistrarTypes(Compilation compilation)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "LayerZero.Migrations"
                    || !attribute.AttributeClass.Name.Equals("MigrationProviderRegistrarAttribute", StringComparison.Ordinal)
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

    private static string RenderSource(
        IEnumerable<ArtifactMetadata> migrations,
        IEnumerable<ArtifactMetadata> seeds,
        IEnumerable<string> registrarTypes,
        IEnumerable<string> providerRegistrarTypes,
        string catalogClassName,
        string registrarClassName,
        string moduleInitializerClassName)
    {
        var orderedMigrations = migrations
            .Distinct()
            .OrderBy(static metadata => metadata.Id, StringComparer.Ordinal)
            .ThenBy(static metadata => metadata.TypeName, StringComparer.Ordinal)
            .ToArray();
        var orderedSeeds = seeds
            .Distinct()
            .OrderBy(static metadata => metadata.Profile.Equals("baseline", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(static metadata => metadata.Profile, StringComparer.Ordinal)
            .ThenBy(static metadata => metadata.Id, StringComparer.Ordinal)
            .ThenBy(static metadata => metadata.TypeName, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS1591");
        builder.AppendLine();
        builder.AppendLine($"[assembly: global::LayerZero.Migrations.MigrationAssemblyRegistrarAttribute(typeof(global::{GeneratedNamespace}.{registrarClassName}))]");
        builder.AppendLine();
        builder.AppendLine($"namespace {GeneratedNamespace}");
        builder.AppendLine("{");
        builder.AppendLine($"    internal sealed class {catalogClassName} : global::LayerZero.Migrations.IMigrationCatalog");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly global::LayerZero.Migrations.MigrationDescriptor[] AllMigrations =");
        builder.AppendLine("        [");

        foreach (var migration in orderedMigrations)
        {
            builder.AppendLine(
                $"            new global::LayerZero.Migrations.MigrationDescriptor(\"{Escape(migration.Id)}\", \"{Escape(migration.Name)}\", typeof({migration.TypeName}), static () => new {migration.TypeName}()),");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::LayerZero.Migrations.SeedDescriptor[] AllSeeds =");
        builder.AppendLine("        [");

        foreach (var seed in orderedSeeds)
        {
            builder.AppendLine(
                $"            new global::LayerZero.Migrations.SeedDescriptor(\"{Escape(seed.Id)}\", \"{Escape(seed.Name)}\", \"{Escape(seed.Profile)}\", typeof({seed.TypeName}), static () => new {seed.TypeName}()),");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Migrations.MigrationDescriptor> Migrations => AllMigrations;");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Migrations.SeedDescriptor> Seeds => AllSeeds;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"    public sealed class {registrarClassName} : global::LayerZero.Migrations.IMigrationAssemblyRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        public void Register(global::LayerZero.Migrations.MigrationAssemblyRegistrationBuilder builder)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(builder);");
        builder.AppendLine($"            builder.AddCatalog<{catalogClassName}>();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    internal static class {moduleInitializerClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Initialize()");
        builder.AppendLine("        {");

        foreach (var registrarType in registrarTypes)
        {
            builder.AppendLine($"            global::LayerZero.Migrations.MigrationAssemblyRegistrarCatalog.Register<{registrarType}>();");
        }

        foreach (var providerRegistrarType in providerRegistrarTypes)
        {
            builder.AppendLine($"            global::LayerZero.Migrations.Internal.MigrationProviderRegistrarCatalog.Register<{providerRegistrarType}>();");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("#pragma warning restore CS1591");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string CreateGeneratedTypeName(string prefix, string? assemblyName)
    {
        return $"{prefix}_{SanitizeIdentifier(assemblyName)}";
    }

    private static string SanitizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Assembly";
        }

        var identifier = value!;
        var builder = new StringBuilder(identifier.Length + 1);
        if (!IsIdentifierStart(identifier[0]))
        {
            builder.Append('_');
        }

        foreach (var character in identifier)
        {
            builder.Append(IsIdentifierPart(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static bool IsIdentifierStart(char character)
    {
        return character == '_' || char.IsLetter(character);
    }

    private static bool IsIdentifierPart(char character)
    {
        return character == '_' || char.IsLetterOrDigit(character);
    }

    private readonly struct ArtifactMetadata
    {
        public ArtifactMetadata(string typeName, string id, string name, string profile, Location? location)
        {
            TypeName = typeName;
            Id = id;
            Name = name;
            Profile = profile;
            Location = location;
        }

        public string TypeName { get; }

        public string Id { get; }

        public string Name { get; }

        public string Profile { get; }

        public Location? Location { get; }
    }
}
