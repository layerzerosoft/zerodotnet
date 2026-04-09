using LayerZero.Testing;
using LayerZero.Validation;

namespace LayerZero.Validation.Tests;

public sealed class ValidatorTests
{
    [Fact]
    public async Task Validator_reports_all_rule_failures()
    {
        CreateProjectValidator validator = new();

        ValidationResult result = await validator.ValidateAsync(
            new CreateProjectRequest(" ", 0),
            TestContext.Current.CancellationToken);

        ValidationFailureCollectionAssertions failures = result.Should().BeInvalid();

        failures.Contain(ValidationCodes.NotEmpty, nameof(CreateProjectRequest.Name));
        failures.Contain("layerzero.validation.capacity_positive", nameof(CreateProjectRequest.Capacity));
    }

    [Fact]
    public async Task Validator_passes_valid_request()
    {
        CreateProjectValidator validator = new();

        ValidationResult result = await validator.ValidateAsync(
            new CreateProjectRequest("Nova", 4),
            TestContext.Current.CancellationToken);

        result.Should().BeValid();
    }

    private sealed record CreateProjectRequest(string? Name, int Capacity);

    private sealed class CreateProjectValidator : Validator<CreateProjectRequest>
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
                    "layerzero.validation.capacity_positive");
        }
    }
}
