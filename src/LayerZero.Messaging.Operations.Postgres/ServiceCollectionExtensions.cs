using LayerZero.Messaging.Operations.Configuration;
using LayerZero.Messaging.Operations.Postgres.Configuration;
using LayerZero.Messaging.Operations.Postgres.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Messaging.Operations.Postgres;

/// <summary>
/// Registers PostgreSQL-backed LayerZero messaging operations services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Uses PostgreSQL-backed messaging operations services.
    /// </summary>
    /// <param name="builder">The messaging operations builder.</param>
    /// <param name="connectionStringName">The logical application connection string name.</param>
    /// <returns>The current builder.</returns>
    public static MessagingOperationsBuilder UsePostgres(
        this MessagingOperationsBuilder builder,
        string connectionStringName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

        builder.Services.AddOptions<PostgresMessagingOperationsOptions>()
            .Configure(options => options.ConnectionStringName = connectionStringName)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "The PostgreSQL messaging operations connection string name must not be empty.")
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<PostgresDeadLetterStore>();
        builder.Services.TryAddSingleton<IDeadLetterStore>(static services => services.GetRequiredService<PostgresDeadLetterStore>());
        builder.Services.TryAddSingleton<IMessageIdempotencyStore, PostgresMessageIdempotencyStore>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMessageSettlementObserver, PostgresDeadLetterSettlementObserver>());

        return builder;
    }
}
