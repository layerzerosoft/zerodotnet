using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using LayerZero.Migrations.Configuration;

namespace LayerZero.Migrations.Internal;

internal sealed class MigrationsOptionsSetup(IConfiguration configuration) : IConfigureOptions<MigrationsOptions>
{
    private readonly IConfiguration configuration = configuration;

    public void Configure(MigrationsOptions options)
    {
        configuration.GetSection("LayerZero:Migrations").Bind(options);
    }
}
