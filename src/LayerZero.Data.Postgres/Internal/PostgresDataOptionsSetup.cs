using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Postgres.Internal;

internal sealed class PostgresDataOptionsSetup(
    IConfiguration? configuration = null) :
    IConfigureOptions<PostgresDataOptions>,
    IPostConfigureOptions<PostgresDataOptions>
{
    private readonly IConfiguration? configuration = configuration;

    public void Configure(PostgresDataOptions options)
    {
        configuration?.GetSection("LayerZero:Data:Postgres").Bind(options);
    }

    public void PostConfigure(string? name, PostgresDataOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ConnectionString = configuration?["LayerZero:Data:ConnectionString"];
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString)
            && !string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            options.ConnectionString = configuration?.GetConnectionString(options.ConnectionStringName);
        }
    }
}
