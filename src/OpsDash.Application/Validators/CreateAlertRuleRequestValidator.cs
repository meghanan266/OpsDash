using FluentValidation;
using OpsDash.Application.DTOs.Alerts;

namespace OpsDash.Application.Validators;

public class CreateAlertRuleRequestValidator : AbstractValidator<CreateAlertRuleRequest>
{
    private const decimal MinThreshold = -999_999_999m;
    private const decimal MaxThreshold = 999_999_999m;

    private static readonly string[] ValidOperators = ["GreaterThan", "LessThan", "Equals"];

    private static readonly string[] ValidModes = ["Current", "Predictive"];

    public CreateAlertRuleRequestValidator()
    {
        RuleFor(x => x.MetricName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Threshold).InclusiveBetween(MinThreshold, MaxThreshold);

        RuleFor(x => x.Operator)
            .NotEmpty()
            .Must(op => ValidOperators.Contains(op))
            .WithMessage("Operator must be GreaterThan, LessThan, or Equals.");

        RuleFor(x => x.AlertMode)
            .NotEmpty()
            .Must(mode => ValidModes.Contains(mode))
            .WithMessage("AlertMode must be Current or Predictive.");

        When(
            x => string.Equals(x.AlertMode, "Predictive", StringComparison.Ordinal),
            () =>
            {
                RuleFor(x => x.ForecastHorizon)
                    .NotNull()
                    .WithMessage("ForecastHorizon is required when AlertMode is Predictive.")
                    .GreaterThan(0)
                    .LessThanOrEqualTo(365);
            });
    }
}
