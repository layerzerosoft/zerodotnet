using LayerZero.ZeroDotNet;
using LayerZero.ZeroDotNet.AspNetCore;
using LayerZero.ZeroDotNet.Validation;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

builder.Services
    .AddZeroDotNet()
    .AddZeroSlice<GetPulseSlice>()
    .AddZeroSlice<CreateWidgetSlice>()
    .AddZeroValidator<CreateWidgetRequest, CreateWidgetRequestValidator>();

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"))
    .ExcludeFromDescription();

app.MapZeroGet<PulseResponse, GetPulseSlice>("/pulse")
    .WithName("GetPulse")
    .WithTags("foundation");

app.MapZeroPostAsync<CreateWidgetRequest, CreateWidgetResponse, CreateWidgetSlice>("/widgets")
    .WithName("CreateWidget")
    .WithTags("widgets");

app.Run();

public partial class Program;

internal sealed class GetPulseSlice : IZeroRequestHandler<ZeroUnit, PulseResponse>
{
    public ZeroResult<PulseResponse> Handle(ZeroUnit request)
    {
        PulseResponse response = new("ready", Environment.Version.ToString(), "net10.0");
        return ZeroResult<PulseResponse>.Success(response);
    }
}

internal sealed class CreateWidgetSlice : IZeroAsyncRequestHandler<CreateWidgetRequest, CreateWidgetResponse>
{
    public ValueTask<ZeroResult<CreateWidgetResponse>> HandleAsync(
        CreateWidgetRequest request,
        CancellationToken cancellationToken = default)
    {
        string name = request.Name!.Trim();
        CreateWidgetResponse response = new(Guid.NewGuid(), name);
        return ValueTask.FromResult(ZeroResult<CreateWidgetResponse>.Success(response));
    }
}

internal sealed class CreateWidgetRequestValidator : ZeroValidator<CreateWidgetRequest>
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
