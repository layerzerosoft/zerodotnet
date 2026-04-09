using LayerZero.Core;
using LayerZero.AspNetCore;
using LayerZero.Validation;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

builder.Services
    .AddLayerZero()
    .AddSlice<GetPulseSlice>()
    .AddSlice<CreateWidgetSlice>()
    .AddValidator<CreateWidgetRequest, CreateWidgetRequestValidator>();

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"))
    .ExcludeFromDescription();

app.MapGetSlice<PulseResponse, GetPulseSlice>("/pulse")
    .WithName("GetPulse")
    .WithTags("foundation");

app.MapPostSliceAsync<CreateWidgetRequest, CreateWidgetResponse, CreateWidgetSlice>("/widgets")
    .WithName("CreateWidget")
    .WithTags("widgets");

app.Run();

public partial class Program;

internal sealed class GetPulseSlice : IRequestHandler<Unit, PulseResponse>
{
    public Result<PulseResponse> Handle(Unit request)
    {
        PulseResponse response = new("ready", Environment.Version.ToString(), "net10.0");
        return Result<PulseResponse>.Success(response);
    }
}

internal sealed class CreateWidgetSlice : IAsyncRequestHandler<CreateWidgetRequest, CreateWidgetResponse>
{
    public ValueTask<Result<CreateWidgetResponse>> HandleAsync(
        CreateWidgetRequest request,
        CancellationToken cancellationToken = default)
    {
        string name = request.Name!.Trim();
        CreateWidgetResponse response = new(Guid.NewGuid(), name);
        return ValueTask.FromResult(Result<CreateWidgetResponse>.Success(response));
    }
}

internal sealed class CreateWidgetRequestValidator : Validator<CreateWidgetRequest>
{
    public CreateWidgetRequestValidator()
    {
        RuleFor(nameof(CreateWidgetRequest.Name), request => request.Name)
            .NotEmpty()
            .MaximumLength(64);
    }
}

internal sealed record CreateWidgetRequest(string? Name);

internal sealed record CreateWidgetResponse(Guid Id, string Name);

internal sealed record PulseResponse(string Status, string Runtime, string Baseline);
