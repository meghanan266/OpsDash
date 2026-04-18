using OpsDash.Application.Interfaces;

namespace OpsDash.Infrastructure.Services;

public class CurrentTenantService : ITenantContextService
{
    public int TenantId { get; set; }
}
