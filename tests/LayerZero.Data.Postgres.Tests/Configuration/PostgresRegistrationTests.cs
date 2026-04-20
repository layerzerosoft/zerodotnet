using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LayerZero.Data.Postgres.Tests.Configuration;

public sealed class PostgresRegistrationTests
{
    [Fact]
    public void UsePostgres_supports_code_first_configuration_without_IConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddData(data => data.UsePostgres(options =>
        {
            options.ConnectionString = "Host=localhost;Database=codefirst;Username=postgres;Password=postgres";
            options.DefaultSchema = "sales";
        }));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<PostgresDataOptions>>().Value;

        Assert.Equal("Host=localhost;Database=codefirst;Username=postgres;Password=postgres", options.ConnectionString);
        Assert.Equal("sales", options.DefaultSchema);
        Assert.NotNull(host.Services.GetRequiredService<NpgsqlDataSource>());
    }

    [Fact]
    public void UsePostgres_prefers_explicit_configuration_over_sections_and_connection_strings()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Host=localhost;Database=connectionstrings;Username=postgres;Password=postgres"),
            new KeyValuePair<string, string?>("LayerZero:Data:Postgres:ConnectionString", "Host=localhost;Database=section;Username=postgres;Password=postgres"),
            new KeyValuePair<string, string?>("LayerZero:Data:Postgres:DefaultSchema", "section"),
        ]);

        builder.Services.AddData(data => data.UsePostgres(options =>
        {
            options.ConnectionString = "Host=localhost;Database=explicit;Username=postgres;Password=postgres";
            options.DefaultSchema = "explicit";
        }));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<PostgresDataOptions>>().Value;

        Assert.Equal("Host=localhost;Database=explicit;Username=postgres;Password=postgres", options.ConnectionString);
        Assert.Equal("explicit", options.DefaultSchema);
    }

    [Fact]
    public void UsePostgres_uses_provider_neutral_connection_string_override_before_connection_strings()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Host=localhost;Database=connectionstrings;Username=postgres;Password=postgres"),
            new KeyValuePair<string, string?>("LayerZero:Data:ConnectionString", "Host=localhost;Database=override;Username=postgres;Password=postgres"),
        ]);

        builder.Services.AddData(data => data.UsePostgres());

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<PostgresDataOptions>>().Value;

        Assert.Equal("Host=localhost;Database=override;Username=postgres;Password=postgres", options.ConnectionString);
        Assert.Equal("public", options.DefaultSchema);
    }

    [Fact]
    public void UsePostgres_named_connection_uses_the_requested_connection_string_and_provider_defaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Fulfillment", "Host=localhost;Database=fulfillment;Username=postgres;Password=postgres"),
        ]);

        builder.Services.AddData(data => data.UsePostgres("Fulfillment"));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<PostgresDataOptions>>().Value;

        Assert.Equal("Host=localhost;Database=fulfillment;Username=postgres;Password=postgres", options.ConnectionString);
        Assert.Equal("Fulfillment", options.ConnectionStringName);
        Assert.Equal("public", options.DefaultSchema);
    }
}
