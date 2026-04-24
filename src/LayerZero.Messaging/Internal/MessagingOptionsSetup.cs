using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Internal;

internal sealed class MessagingOptionsSetup(
    IConfiguration? configuration = null,
    IHostEnvironment? environment = null) :
    IConfigureOptions<MessagingOptions>,
    IPostConfigureOptions<MessagingOptions>
{
    private readonly IConfiguration? configuration = configuration;
    private readonly IHostEnvironment? environment = environment;

    public void Configure(MessagingOptions options)
    {
        configuration?.GetSection("Messaging").Bind(options);
    }

    public void PostConfigure(string? name, MessagingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            options.ApplicationName = environment?.ApplicationName;
        }
    }
}
