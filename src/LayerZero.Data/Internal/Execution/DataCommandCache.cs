using System.Collections.Concurrent;

namespace LayerZero.Data.Internal.Execution;

internal sealed class DataCommandCache
{
    private readonly ConcurrentDictionary<string, object> templates = new(StringComparer.Ordinal);

    public TTemplate GetOrAdd<TTemplate>(string key, Func<TTemplate> factory)
        where TTemplate : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        return (TTemplate)templates.GetOrAdd(key, static (_, state) => state(), factory);
    }
}
