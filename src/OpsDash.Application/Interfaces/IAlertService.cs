using OpsDash.Application.DTOs.Alerts;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.Application.Interfaces;

public interface IAlertService
{
    Task<ApiResponse<PagedResult<AlertRuleDto>>> GetAlertRulesAsync(PagedRequest paging);

    Task<ApiResponse<AlertRuleDto>> CreateAlertRuleAsync(CreateAlertRuleRequest request, int userId);

    Task<ApiResponse<AlertRuleDto>> UpdateAlertRuleAsync(int id, UpdateAlertRuleRequest request);

    Task<ApiResponse<bool>> DeleteAlertRuleAsync(int id);

    Task<ApiResponse<PagedResult<AlertDto>>> GetAlertsAsync(PagedRequest paging);

    Task<ApiResponse<bool>> AcknowledgeAlertAsync(long id, int userId);
}
