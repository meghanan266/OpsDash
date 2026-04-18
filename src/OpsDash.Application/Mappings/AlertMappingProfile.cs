using AutoMapper;
using OpsDash.Application.DTOs.Alerts;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Mappings;

public class AlertMappingProfile : Profile
{
    public AlertMappingProfile()
    {
        CreateMap<AlertRule, AlertRuleDto>()
            .ForMember(
                d => d.CreatedByName,
                o => o.MapFrom(s => s.CreatedByUser.FirstName + " " + s.CreatedByUser.LastName));

        CreateMap<Alert, AlertDto>()
            .ForMember(d => d.MetricName, o => o.MapFrom(s => s.AlertRule.MetricName))
            .ForMember(d => d.Threshold, o => o.MapFrom(s => s.AlertRule.Threshold))
            .ForMember(d => d.Operator, o => o.MapFrom(s => s.AlertRule.Operator))
            .ForMember(
                d => d.AcknowledgedByName,
                o => o.MapFrom(s =>
                    s.AcknowledgedByUser == null
                        ? null
                        : s.AcknowledgedByUser.FirstName + " " + s.AcknowledgedByUser.LastName));

        CreateMap<CreateAlertRuleRequest, AlertRule>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.TenantId, o => o.Ignore())
            .ForMember(d => d.IsActive, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.Tenant, o => o.Ignore())
            .ForMember(d => d.CreatedByUser, o => o.Ignore());
    }
}
