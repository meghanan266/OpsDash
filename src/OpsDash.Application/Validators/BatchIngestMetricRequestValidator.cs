using FluentValidation;
using OpsDash.Application.DTOs.Metrics;

namespace OpsDash.Application.Validators;

public class BatchIngestMetricRequestValidator : AbstractValidator<BatchIngestMetricRequest>
{
    public BatchIngestMetricRequestValidator()
    {
        RuleFor(x => x.Metrics)
            .NotEmpty()
            .Must(list => list.Count <= 1000)
            .WithMessage("Metrics list cannot exceed 1000 items.");

        RuleForEach(x => x.Metrics)
            .SetValidator(new IngestMetricRequestValidator());
    }
}

