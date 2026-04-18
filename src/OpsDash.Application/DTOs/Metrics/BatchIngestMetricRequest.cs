namespace OpsDash.Application.DTOs.Metrics;

public class BatchIngestMetricRequest
{
    public List<IngestMetricRequest> Metrics { get; set; } = [];
}

