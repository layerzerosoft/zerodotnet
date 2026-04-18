using LayerZero.Data.Configuration;
using LayerZero.Data.SqlServer;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Data.SqlServer.Internal.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Tests;

internal static class TestDataServices
{
    public static ServiceProvider BuildProvider(Action<DataBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddData(data =>
        {
            configure?.Invoke(data);
            data.UseSqlServer(options =>
            {
                options.ConnectionString = "Server=(local);Database=fake;User Id=sa;Password=Password1!;";
                options.DefaultSchema = "dbo";
            });
        });

        return services.BuildServiceProvider();
    }

    public static SqlServerDataSqlDialect CreateDialect() =>
        new(Options.Create(new SqlServerDataOptions
        {
            ConnectionString = "Server=(local);Database=fake;User Id=sa;Password=Password1!;",
            ConnectionStringName = "Default",
            DefaultSchema = "dbo",
        }));
}
