using LayerZero.Client;
using Microsoft.Extensions.DependencyInjection;
using LayerZero.MinimalApi.Client.Sample.Clients;
using LayerZero.MinimalApi.Contracts.Todos;

namespace LayerZero.MinimalApi.Client.Sample;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var baseAddress = new Uri(args.FirstOrDefault() ?? "http://localhost:5270", UriKind.Absolute);

        var services = new ServiceCollection();
        services.AddLayerZeroClient<TodosClient>(client =>
        {
            client.BaseAddress = baseAddress;
        });

        using var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<TodosClient>();

        var listed = await api.ListAsync(includeCompleted: true);
        if (listed.IsFailure)
        {
            Console.Error.WriteLine("List failed:");
            foreach (var error in listed.Errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return;
        }

        Console.WriteLine($"Existing todos: {listed.Value.Count}");

        var created = await api.CreateAsync(new CreateTodo.Request(
            "Ship LayerZero client",
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7))));

        if (created.IsFailure)
        {
            Console.Error.WriteLine("Create failed:");
            foreach (var error in created.Errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return;
        }

        Console.WriteLine($"Created todo {created.Value.Id}: {created.Value.Title}");

        var completed = await api.CompleteAsync(created.Value.Id);
        Console.WriteLine(completed.IsSuccess
            ? $"Completed todo {completed.Value.Id}"
            : $"Complete failed: {string.Join(", ", completed.Errors.Select(error => error.Code))}");

        var fetched = await api.GetForResponseAsync(created.Value.Id);
        Console.WriteLine($"Fetch status: {(int)fetched.StatusCode}");
    }
}
