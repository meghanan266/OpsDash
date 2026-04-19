using AutoMapper;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.HealthScores;
using OpsDash.Application.DTOs.Incidents;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Mappings;

public class DashboardMappingProfile : Profile
{
    public DashboardMappingProfile()
    {
        CreateMap<HealthScore, HealthScoreDto>();
        CreateMap<AnomalyScore, AnomalyDto>();
        CreateMap<AnomalyScore, AnomalyDetailDto>()
            .ForMember(d => d.Correlations, o => o.Ignore());
        CreateMap<MetricCorrelation, MetricCorrelationDto>();
        CreateMap<Incident, IncidentDto>();
        CreateMap<Incident, IncidentDetailDto>()
            .IncludeBase<Incident, IncidentDto>()
            .ForMember(d => d.Events, o => o.Ignore());
        CreateMap<IncidentEvent, IncidentEventDto>();
    }
}
