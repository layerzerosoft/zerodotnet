using System.Text;

namespace LayerZero.Messaging;

/// <summary>
/// Provides deterministic topology naming helpers shared by transports.
/// </summary>
public static class MessageTopologyNames
{
    /// <summary>
    /// Normalizes one logical topology segment.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized segment.</returns>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('.');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('.');
    }

    /// <summary>
    /// Resolves the default command or event entity name.
    /// </summary>
    /// <param name="kind">The message kind.</param>
    /// <param name="messageName">The logical message name.</param>
    /// <returns>The entity name.</returns>
    public static string Entity(MessageKind kind, string messageName)
    {
        return kind == MessageKind.Command
            ? $"lz.cmd.{Normalize(messageName)}"
            : $"lz.evt.{Normalize(messageName)}";
    }

    /// <summary>
    /// Resolves a deterministic subscription or consumer name.
    /// </summary>
    /// <param name="applicationName">The application name.</param>
    /// <param name="handlerIdentity">The handler identity.</param>
    /// <returns>The subscription name.</returns>
    public static string Subscription(string applicationName, string handlerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerIdentity);
        return $"lz.sub.{Normalize(applicationName)}.{Normalize(handlerIdentity)}";
    }

    /// <summary>
    /// Resolves a dead-letter entity name.
    /// </summary>
    /// <param name="entityName">The base entity name.</param>
    /// <returns>The dead-letter entity name.</returns>
    public static string DeadLetter(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        return $"{entityName}.deadletter";
    }

    /// <summary>
    /// Resolves a retry entity name.
    /// </summary>
    /// <param name="entityName">The base entity name.</param>
    /// <param name="tier">The retry tier.</param>
    /// <returns>The retry entity name.</returns>
    public static string Retry(string entityName, string tier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tier);
        return $"{entityName}.retry.{Normalize(tier)}";
    }
}
