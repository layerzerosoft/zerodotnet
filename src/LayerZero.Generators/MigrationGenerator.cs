using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LayerZero.Generators;

/// <summary>
/// Generates LayerZero migration registration extensions and registries.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MigrationGenerator : IIncrementalGenerator
{
    private const string MigrationsNamespace = "LayerZero.Migrations";
    private const string MigrationsExtensionsClassName = "LayerZeroGeneratedMigrationExtensions";
    private const string MigrationsRegistryClassName = "LayerZeroGeneratedMigrationRegistry";
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly DiagnosticDescriptor UnsupportedMigrationShape = new(
        "LZGEN008",
        "Unsupported migration artifact shape",
        "Migration artifact '{0}' must be a non-generic concrete class with an accessible parameterless constructor that calls the LayerZero base constructor with constant metadata",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMigrationId = new(
        "LZGEN009",
        "Duplicate migration ids are not supported",
        "Migrations '{0}' and '{1}' resolve to the same id '{2}'",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateSeedId = new(
        "LZGEN010",
        "Duplicate seed ids in one profile are not supported",
        "Seeds '{0}' and '{1}' resolve to the same profile/id '{2}/{3}'",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSeedProfile = new(
        "LZGEN011",
        "Invalid seed profile metadata",
        "Seed '{0}' uses profile '{1}', but profiles must use lowercase letters, numbers, or dashes",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MigrationExtensionCollision = new(
        "LZGEN012",
        "Generated migration extension collision",
        "The type '{0}.{1}' already exists; rename it so LayerZero can generate migration extensions",
        "LayerZero.Migrations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidMigrationId = new(
        "LZGEN013",
        "Invalid migration artifact id",
        "Migration artifact '{0}' uses id '{1}', but ids must be 14-digit UTC timestamps like yyyyMMddHHmmss",
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
        if (compilation.GetTypeByMetadataName("LayerZero.Migrations.IMigrationRegistry") is null)
        {
            return;
        }

        if (HasGeneratedExtensionCollision(compilation, MigrationsNamespace, MigrationsExtensionsClassName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MigrationExtensionCollision,
                Location.None,
                MigrationsNamespace,
                MigrationsExtensionsClassName));
            return;
        }

        var migrations = new List<MigrationArtifactMetadata>();
        var seeds = new List<MigrationArtifactMetadata>();

        foreach (var symbol in candidateTypes.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            if (IsDerivedFrom(symbol, "LayerZero.Migrations", "Migration"))
            {
                if (!TryCreateMetadata(compilation, symbol, isSeed: false, out var metadata))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMigrationShape,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString()));
                    continue;
                }

                if (!IsValidId(metadata.Id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidMigrationId,
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
                if (!TryCreateMetadata(compilation, symbol, isSeed: true, out var metadata))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMigrationShape,
                        symbol.Locations.FirstOrDefault(),
                        symbol.ToDisplayString()));
                    continue;
                }

                if (!IsValidId(metadata.Id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidMigrationId,
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

        var sourceText = RenderSource(
            migrations
                .Distinct()
                .OrderBy(static metadata => metadata.Id, StringComparer.Ordinal)
                .ThenBy(static metadata => metadata.TypeName, StringComparer.Ordinal),
            seeds
                .Distinct()
                .OrderBy(static metadata => metadata.Profile.Equals("baseline", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(static metadata => metadata.Profile, StringComparer.Ordinal)
                .ThenBy(static metadata => metadata.Id, StringComparer.Ordinal)
                .ThenBy(static metadata => metadata.TypeName, StringComparer.Ordinal));

        context.AddSource("LayerZero.Migrations.g.cs", SourceText.From(sourceText, Encoding.UTF8));
    }

    private static bool TryCreateMetadata(
        Compilation compilation,
        INamedTypeSymbol symbol,
        bool isSeed,
        out MigrationArtifactMetadata metadata)
    {
        metadata = default;

        if (!IsConcrete(symbol))
        {
            return false;
        }

        var parameterlessConstructor = symbol.InstanceConstructors.FirstOrDefault(static constructor =>
            !constructor.IsImplicitlyDeclared
            && constructor.Parameters.Length == 0
            && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
        if (parameterlessConstructor is null)
        {
            return false;
        }

        foreach (var syntaxReference in parameterlessConstructor.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not ConstructorDeclarationSyntax constructorSyntax
                || constructorSyntax.Initializer is not { RawKind: (int)SyntaxKind.BaseConstructorInitializer } initializer)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(constructorSyntax.SyntaxTree);
            var arguments = initializer.ArgumentList.Arguments;
            if (arguments.Count < 2)
            {
                continue;
            }

            var id = semanticModel.GetConstantValue(arguments[0].Expression);
            var name = semanticModel.GetConstantValue(arguments[1].Expression);

            if (!id.HasValue || id.Value is not string idValue || string.IsNullOrWhiteSpace(idValue)
                || !name.HasValue || name.Value is not string nameValue || string.IsNullOrWhiteSpace(nameValue))
            {
                continue;
            }

            if (isSeed)
            {
                var profile = "baseline";
                if (arguments.Count >= 3)
                {
                    var profileValue = semanticModel.GetConstantValue(arguments[2].Expression);
                    if (!profileValue.HasValue || profileValue.Value is not string explicitProfile || string.IsNullOrWhiteSpace(explicitProfile))
                    {
                        continue;
                    }

                    profile = explicitProfile;
                }

                if (string.IsNullOrWhiteSpace(profile))
                {
                    continue;
                }

                metadata = new MigrationArtifactMetadata(
                    symbol.ToDisplayString(FullyQualifiedFormat),
                    idValue,
                    nameValue,
                    profile,
                    "global::LayerZero.Migrations.MigrationTransactionMode.Transactional",
                    symbol.Locations.FirstOrDefault());
                return true;
            }

            var modeValue = 0;
            if (arguments.Count >= 3)
            {
                var transactionMode = semanticModel.GetConstantValue(arguments[2].Expression);
                if (!transactionMode.HasValue || transactionMode.Value is not int explicitModeValue)
                {
                    continue;
                }

                modeValue = explicitModeValue;
            }

            metadata = new MigrationArtifactMetadata(
                symbol.ToDisplayString(FullyQualifiedFormat),
                idValue,
                nameValue,
                string.Empty,
                modeValue == 1
                    ? "global::LayerZero.Migrations.MigrationTransactionMode.NonTransactional"
                    : "global::LayerZero.Migrations.MigrationTransactionMode.Transactional",
                symbol.Locations.FirstOrDefault());
            return true;
        }

        return false;
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

    private static bool HasGeneratedExtensionCollision(Compilation compilation, string @namespace, string typeName)
    {
        var namespaceParts = @namespace.Split('.');
        var current = compilation.GlobalNamespace;

        foreach (var part in namespaceParts)
        {
            current = current?.GetNamespaceMembers().FirstOrDefault(candidate => candidate.Name.Equals(part, StringComparison.Ordinal));
            if (current is null)
            {
                return false;
            }
        }

        return current.GetTypeMembers(typeName).Any();
    }

    private static string RenderSource(
        IEnumerable<MigrationArtifactMetadata> migrations,
        IEnumerable<MigrationArtifactMetadata> seeds)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {MigrationsNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers source-generated LayerZero migration artifacts.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"LayerZero.Generators\", \"0.1.0-alpha.1\")]");
        builder.AppendLine($"    public static partial class {MigrationsExtensionsClassName}");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Adds the generated LayerZero migration registry to the service collection.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        /// <param name=\"services\">The service collection to configure.</param>");
        builder.AppendLine("        /// <returns>The service collection for chaining.</returns>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddMigrations(");
        builder.AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<global::LayerZero.Migrations.IMigrationRegistry, global::LayerZero.Migrations.LayerZeroGeneratedMigrationRegistry>(services);");
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    internal sealed class {MigrationsRegistryClassName} : global::LayerZero.Migrations.IMigrationRegistry");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly global::LayerZero.Migrations.MigrationDescriptor[] AllMigrations =");
        builder.AppendLine("        [");

        foreach (var migration in migrations)
        {
            builder.AppendLine($"            new global::LayerZero.Migrations.MigrationDescriptor(\"{Escape(migration.Id)}\", \"{Escape(migration.Name)}\", typeof({migration.TypeName}), {migration.TransactionModeExpression}, static () => new {migration.TypeName}()),");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::LayerZero.Migrations.SeedDescriptor[] AllSeeds =");
        builder.AppendLine("        [");

        foreach (var seed in seeds)
        {
            builder.AppendLine($"            new global::LayerZero.Migrations.SeedDescriptor(\"{Escape(seed.Id)}\", \"{Escape(seed.Name)}\", \"{Escape(seed.Profile)}\", typeof({seed.TypeName}), static () => new {seed.TypeName}()),");
        }

        builder.AppendLine("        ];");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Migrations.MigrationDescriptor> Migrations => AllMigrations;");
        builder.AppendLine();
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::LayerZero.Migrations.SeedDescriptor> Seeds => AllSeeds;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private readonly struct MigrationArtifactMetadata : IEquatable<MigrationArtifactMetadata>
    {
        public MigrationArtifactMetadata(
            string typeName,
            string id,
            string name,
            string profile,
            string transactionModeExpression,
            Location? location)
        {
            TypeName = typeName;
            Id = id;
            Name = name;
            Profile = profile;
            TransactionModeExpression = transactionModeExpression;
            Location = location;
        }

        public string TypeName { get; }

        public string Id { get; }

        public string Name { get; }

        public string Profile { get; }

        public string TransactionModeExpression { get; }

        public Location? Location { get; }

        public bool Equals(MigrationArtifactMetadata other)
        {
            return TypeName.Equals(other.TypeName, StringComparison.Ordinal)
                && Id.Equals(other.Id, StringComparison.Ordinal)
                && Name.Equals(other.Name, StringComparison.Ordinal)
                && Profile.Equals(other.Profile, StringComparison.Ordinal)
                && TransactionModeExpression.Equals(other.TransactionModeExpression, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is MigrationArtifactMetadata other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeName);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Id);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Profile);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TransactionModeExpression);
                return hash;
            }
        }
    }

    private readonly struct Optional
    {
        private Optional(object? value)
        {
            Value = value;
            HasValue = true;
        }

        public bool HasValue { get; }

        public object? Value { get; }

        public static Optional FromValue(object? value) => new(value);
    }
}
