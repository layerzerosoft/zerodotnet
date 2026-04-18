using LayerZero.Migrations.Postgres.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.Postgres.Internal;

internal sealed class PostgresMigrationsOptionsSetup(IConfiguration configuration) : IConfigureOptions<PostgresMigrationsOptions>
{
    private readonly IConfiguration configuration = configuration;

    public void Configure(PostgresMigrationsOptions options)
    {
        configuration.GetSection("LayerZero:Migrations:Postgres").Bind(options);
    }
}
