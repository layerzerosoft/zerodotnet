using LayerZero.ZeroDotNet.Testing;
using LayerZero.ZeroDotNet.Validation;

namespace LayerZero.ZeroDotNet.Validation.Tests;

public sealed class ZeroValidatorTests
{
    [Fact]
    public async Task Validator_reports_all_rule_failures()
    {
        CreateProjectValidator validator = new();

        ZeroValidationResult result = await validator.ValidateAsync(
            new CreateProjectRequest(" ", 0),
            TestContext.Current.CancellationToken);

        IReadOnlyList<ZeroValidationFailure> errors = ZeroAssert.Invalid(result);

        Assert.Contains(errors, error => error.PropertyName == nameof(CreateProjectRequest.Name)
            && error.Code == ZeroValidationCodes.NotEmpty);
        Assert.Contains(errors, error => error.PropertyName == nameof(CreateProjectRequest.Capacity)
            && error.Code == "zero.validation.capacity_positive");
    }

    [Fact]
    public async Task Validator_passes_valid_request()
    {
        CreateProjectValidator validator = new();

        ZeroValidationResult result = await validator.ValidateAsync(
            new CreateProjectRequest("Nova", 4),
            TestContext.Current.CancellationToken);

        ZeroAssert.Valid(result);
    }

    private sealed record CreateProjectRequest(string? Name, int Capacity);

    private sealed class CreateProjectValidator : ZeroValidator<CreateProjectRequest>
    {
        public CreateProjectValidator()
        {
            RuleFor(nameof(CreateProjectRequest.Name), request => request.Name)
                .NotEmpty()
                .MaximumLength(12);

            RuleFor(nameof(CreateProjectRequest.Capacity), request => request.Capacity)
                .Must(
                    capacity => capacity > 0,
                    "Capacity must be positive.",
                    "zero.validation.capacity_positive");
        }
    }
}
