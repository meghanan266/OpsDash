using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Incidents;

namespace OpsDash.Application.Interfaces;

public interface IIncidentService
{
    Task<ApiResponse<PagedResult<IncidentDto>>> GetIncidentsAsync(PagedRequest paging);

    Task<ApiResponse<IncidentDetailDto>> GetByIdAsync(int id);

    Task<ApiResponse<IncidentDto>> AcknowledgeAsync(int id, int userId);

    Task<ApiResponse<IncidentDto>> UpdateStatusAsync(int id, string status);
}
