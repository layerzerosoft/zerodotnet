using LayerZero.Data.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Internal;

internal sealed class DataOptionsSetup(IConfiguration? configuration = null) : IConfigureOptions<DataOptions>
{
    private readonly IConfiguration? configuration = configuration;

    public void Configure(DataOptions options)
    {
        configuration?.GetSection("LayerZero:Data").Bind(options);
    }
}
