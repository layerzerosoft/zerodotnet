using LayerZero.Data.Configuration;
using LayerZero.Data.SqlServer.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Internal;

internal sealed class SqlServerDataOptionsSetup(
    IConfiguration configuration,
    IOptions<LayerZeroDataOptions> dataOptionsAccessor) :
    IConfigureOptions<SqlServerDataOptions>,
    IPostConfigureOptions<SqlServerDataOptions>
{
    private readonly IConfiguration configuration = configuration;
    private readonly LayerZeroDataOptions dataOptions = dataOptionsAccessor.Value;

    public void Configure(SqlServerDataOptions options)
    {
        configuration.GetSection("LayerZero:Data:SqlServer").Bind(options);
    }

    public void PostConfigure(string? name, SqlServerDataOptions options)
    {
        var connectionStringName = string.IsNullOrWhiteSpace(options.ConnectionStringName)
            ? dataOptions.ConnectionStringName
            : options.ConnectionStringName;

        options.ConnectionStringName = connectionStringName;

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ConnectionString = configuration.GetConnectionString(connectionStringName);
        }
    }
}
