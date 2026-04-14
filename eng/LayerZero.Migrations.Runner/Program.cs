using LayerZero.Data;
using LayerZero.Data.SqlServer;
using LayerZero.Migrations;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddLayerZeroData(options =>
    {
        options.ConnectionStringName = "Migrations";
    })
    .UseSqlServer()
    .UseMigrations(options =>
    {
        options.Executor = "layerzero-migrations-runner";
    });

if (await builder.RunLayerZeroMigrationsCommandAsync(args, builder.Build) is { } exitCode)
{
    return exitCode;
}

Console.Error.WriteLine("Use the 'migrations' command. Example: dotnet run --project eng/LayerZero.Migrations.Runner -- migrations info");
return 1;
