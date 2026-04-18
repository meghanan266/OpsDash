using OpsDash.Application.Interfaces;

namespace OpsDash.Infrastructure.Services;

public class CurrentTenantService : ITenantContextService, ICurrentTenantSetter
{
    public int TenantId { get; private set; }

    public void SetTenantId(int tenantId) => TenantId = tenantId;
}
