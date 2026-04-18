using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Migrations.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.SqlServer.Internal;

internal sealed class SqlServerMigrationsRuntimeOptionsSetup(IOptions<SqlServerDataOptions> dataOptionsAccessor) : IPostConfigureOptions<MigrationsOptions>
{
    private readonly SqlServerDataOptions dataOptions = dataOptionsAccessor.Value;

    public void PostConfigure(string? name, MigrationsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HistoryTableSchema))
        {
            options.HistoryTableSchema = dataOptions.DefaultSchema;
        }
    }
}
