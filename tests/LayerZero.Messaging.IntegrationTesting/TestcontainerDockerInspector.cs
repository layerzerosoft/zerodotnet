using System.Diagnostics;
using System.Text.Json;

namespace LayerZero.Messaging.IntegrationTesting;

internal static class TestcontainerDockerInspector
{
    public static async Task<IReadOnlyDictionary<string, string>> GetLabelsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var command = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            command.ArgumentList.Add("inspect");
            command.ArgumentList.Add(containerId);

            using var process = Process.Start(command);
            if (process is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var labelsElement = document.RootElement[0]
                .GetProperty("Config")
                .GetProperty("Labels");

            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in labelsElement.EnumerateObject())
            {
                labels[property.Name] = property.Value.GetString() ?? string.Empty;
            }

            return labels;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
