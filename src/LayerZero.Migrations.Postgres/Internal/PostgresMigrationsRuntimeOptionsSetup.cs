using LayerZero.Data.Postgres.Configuration;
using LayerZero.Migrations.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.Postgres.Internal;

internal sealed class PostgresMigrationsRuntimeOptionsSetup(IOptions<PostgresDataOptions> dataOptionsAccessor) : IPostConfigureOptions<MigrationsOptions>
{
    private readonly PostgresDataOptions dataOptions = dataOptionsAccessor.Value;

    public void PostConfigure(string? name, MigrationsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HistoryTableSchema))
        {
            options.HistoryTableSchema = dataOptions.DefaultSchema;
        }
    }
}
