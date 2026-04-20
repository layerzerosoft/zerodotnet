using LayerZero.Data.SqlServer.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Tests.Configuration;

public sealed class SqlServerRegistrationTests
{
    [Fact]
    public void UseSqlServer_supports_code_first_configuration_without_IConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddData(data => data.UseSqlServer(options =>
        {
            options.ConnectionString = "Server=(local);Database=CodeFirst;Trusted_Connection=True;";
            options.DefaultSchema = "sales";
        }));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<SqlServerDataOptions>>().Value;

        Assert.Equal("Server=(local);Database=CodeFirst;Trusted_Connection=True;", options.ConnectionString);
        Assert.Equal("sales", options.DefaultSchema);
        Assert.Equal("Default", options.ConnectionStringName);
    }

    [Fact]
    public void UseSqlServer_prefers_explicit_configuration_over_sections_and_connection_strings()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Server=(local);Database=ConnectionStrings;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:ConnectionString", "Server=(local);Database=Section;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:DefaultSchema", "section"),
        ]);

        builder.Services.AddData(data => data.UseSqlServer(options =>
        {
            options.ConnectionString = "Server=(local);Database=Explicit;Trusted_Connection=True;";
            options.DefaultSchema = "explicit";
        }));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<SqlServerDataOptions>>().Value;

        Assert.Equal("Server=(local);Database=Explicit;Trusted_Connection=True;", options.ConnectionString);
        Assert.Equal("explicit", options.DefaultSchema);
    }

    [Fact]
    public void UseSqlServer_uses_section_values_before_connection_string_name_lookup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Server=(local);Database=ConnectionStrings;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:ConnectionString", "Server=(local);Database=Section;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:DefaultSchema", "section"),
        ]);

        builder.Services.AddData(data => data.UseSqlServer());

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<SqlServerDataOptions>>().Value;

        Assert.Equal("Server=(local);Database=Section;Trusted_Connection=True;", options.ConnectionString);
        Assert.Equal("section", options.DefaultSchema);
    }

    [Fact]
    public void UseSqlServer_named_connection_uses_the_requested_connection_string_and_provider_defaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Fulfillment", "Server=(local);Database=Fulfillment;Trusted_Connection=True;"),
        ]);

        builder.Services.AddData(data => data.UseSqlServer("Fulfillment"));

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<SqlServerDataOptions>>().Value;

        Assert.Equal("Server=(local);Database=Fulfillment;Trusted_Connection=True;", options.ConnectionString);
        Assert.Equal("Fulfillment", options.ConnectionStringName);
        Assert.Equal("dbo", options.DefaultSchema);
    }
}
