namespace OpsDash.Application.Configuration;

public sealed class AnomalyDetectionSettings
{
    public int BaselineDataPoints { get; set; } = 30;

    public double ZScoreThreshold { get; set; } = 2.0;

    public double WarningSeverityMin { get; set; } = 2.0;

    public double CriticalSeverityMin { get; set; } = 2.5;

    public double SevereSeverityMin { get; set; } = 3.0;
}
