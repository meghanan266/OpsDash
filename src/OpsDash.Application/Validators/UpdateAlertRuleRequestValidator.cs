using FluentValidation;
using OpsDash.Application.DTOs.Alerts;

namespace OpsDash.Application.Validators;

public class UpdateAlertRuleRequestValidator : AbstractValidator<UpdateAlertRuleRequest>
{
    private static readonly string[] ValidOperators = ["GreaterThan", "LessThan", "Equals"];

    private static readonly string[] ValidModes = ["Current", "Predictive"];

    public UpdateAlertRuleRequestValidator()
    {
        When(x => x.MetricName is not null, () => RuleFor(x => x.MetricName!).NotEmpty().MaximumLength(200));

        When(
            x => x.Operator is not null,
            () =>
                RuleFor(x => x.Operator!)
                    .Must(op => ValidOperators.Contains(op))
                    .WithMessage("Operator must be GreaterThan, LessThan, or Equals."));

        When(
            x => x.AlertMode is not null,
            () =>
                RuleFor(x => x.AlertMode!)
                    .Must(mode => ValidModes.Contains(mode))
                    .WithMessage("AlertMode must be Current or Predictive."));

        When(x => x.ForecastHorizon.HasValue, () => RuleFor(x => x.ForecastHorizon!.Value).GreaterThan(0));

        When(
            x => x.AlertMode is not null && string.Equals(x.AlertMode, "Predictive", StringComparison.Ordinal),
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
