using Application.Common.Models;
using Application.Features.Dashboard;
using Application.Features.Dashboard.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Team-wide aggregation views — restricted to Manager/Admin, since every metric here
/// (compliance rate, workload by project, activity feed) is about oversight across the
/// team rather than a single team member's own data.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.ManagerOrAdminCsv)]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] DateOnly? week, CancellationToken ct)
    {
        var result = await _dashboardService.GetSummaryAsync(week, ct);
        return Ok(result);
    }

    [HttpGet("tasks-trend")]
    public async Task<ActionResult<IReadOnlyList<TasksTrendPointDto>>> GetTasksTrend([FromQuery] TasksTrendQuery query, CancellationToken ct)
    {
        var result = await _dashboardService.GetTasksTrendAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("status-by-member")]
    public async Task<ActionResult<IReadOnlyList<StatusByMemberDto>>> GetStatusByMember([FromQuery] DateOnly? week, CancellationToken ct)
    {
        var result = await _dashboardService.GetStatusByMemberAsync(week, ct);
        return Ok(result);
    }

    [HttpGet("workload-by-project")]
    public async Task<ActionResult<IReadOnlyList<WorkloadByProjectDto>>> GetWorkloadByProject([FromQuery] DateRangeQuery query, CancellationToken ct)
    {
        var result = await _dashboardService.GetWorkloadByProjectAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("time-by-task-type")]
    public async Task<ActionResult<IReadOnlyList<TimeByTaskTypeDto>>> GetTimeByTaskType([FromQuery] DateRangeQuery query, CancellationToken ct)
    {
        var result = await _dashboardService.GetTimeByTaskTypeAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("activity-feed")]
    public async Task<ActionResult<PagedResult<ActivityFeedItemDto>>> GetActivityFeed([FromQuery] ActivityFeedQuery query, CancellationToken ct)
    {
        var result = await _dashboardService.GetActivityFeedAsync(query, ct);
        return Ok(result);
    }
}
