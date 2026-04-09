using LayerZero.AspNetCore;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

builder.Services
    .AddSingleton(TimeProvider.System)
    .AddSingleton<ITodoRepository, InMemoryTodoRepository>()
    .AddLayerZero()
    .AddSlices();

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"))
    .ExcludeFromDescription();

app.MapSlices();

app.Run();

public partial class Program;
