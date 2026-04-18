using FluentValidation;
using OpsDash.Application.DTOs.Metrics;

namespace OpsDash.Application.Validators;

public class IngestMetricRequestValidator : AbstractValidator<IngestMetricRequest>
{
    private const decimal MinMetricValue = -999_999_999m;
    private const decimal MaxMetricValue = 999_999_999m;

    public IngestMetricRequestValidator()
    {
        RuleFor(x => x.MetricName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.MetricValue)
            .InclusiveBetween(MinMetricValue, MaxMetricValue);

        When(x => x.RecordedAt.HasValue, () =>
        {
            RuleFor(x => x.RecordedAt!.Value)
                .Must(d => d <= DateTime.UtcNow.AddMinutes(5))
                .WithMessage("RecordedAt cannot be in the future (allowing up to 5 minutes clock skew).");
        });
    }
}

