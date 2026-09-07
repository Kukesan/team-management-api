using Application.Common.Models;
using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? week, CancellationToken ct = default);
    Task<IReadOnlyList<TasksTrendPointDto>> GetTasksTrendAsync(TasksTrendQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<StatusByMemberDto>> GetStatusByMemberAsync(DateOnly? week, CancellationToken ct = default);
    Task<IReadOnlyList<WorkloadByProjectDto>> GetWorkloadByProjectAsync(DateRangeQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TimeByTaskTypeDto>> GetTimeByTaskTypeAsync(DateRangeQuery query, CancellationToken ct = default);
    Task<PagedResult<ActivityFeedItemDto>> GetActivityFeedAsync(ActivityFeedQuery query, CancellationToken ct = default);
}
