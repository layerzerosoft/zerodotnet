using LayerZero.Migrations.SqlServer.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.SqlServer.Internal;

internal sealed class SqlServerMigrationsOptionsSetup(IConfiguration configuration) : IConfigureOptions<SqlServerMigrationsOptions>
{
    private readonly IConfiguration configuration = configuration;

    public void Configure(SqlServerMigrationsOptions options)
    {
        configuration.GetSection("LayerZero:Migrations:SqlServer").Bind(options);
    }
}
