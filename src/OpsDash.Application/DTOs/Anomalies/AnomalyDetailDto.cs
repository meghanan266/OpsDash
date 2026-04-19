namespace OpsDash.Application.DTOs.Anomalies;

public class AnomalyDetailDto : AnomalyDto
{
    public List<MetricCorrelationDto> Correlations { get; set; } = [];
}
