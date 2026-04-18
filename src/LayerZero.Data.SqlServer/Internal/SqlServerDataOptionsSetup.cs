using LayerZero.Data.SqlServer.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Internal;

internal sealed class SqlServerDataOptionsSetup(
    IConfiguration? configuration = null) :
    IConfigureOptions<SqlServerDataOptions>,
    IPostConfigureOptions<SqlServerDataOptions>
{
    private readonly IConfiguration? configuration = configuration;

    public void Configure(SqlServerDataOptions options)
    {
        configuration?.GetSection("LayerZero:Data:SqlServer").Bind(options);
    }

    public void PostConfigure(string? name, SqlServerDataOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString)
            && !string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            options.ConnectionString = configuration?.GetConnectionString(options.ConnectionStringName);
        }
    }
}
