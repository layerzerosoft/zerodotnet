using LayerZero.Data.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Internal;

internal sealed class LayerZeroDataOptionsSetup(IConfiguration configuration) : IConfigureOptions<LayerZeroDataOptions>
{
    private readonly IConfiguration configuration = configuration;

    public void Configure(LayerZeroDataOptions options)
    {
        configuration.GetSection("LayerZero:Data").Bind(options);
    }
}
