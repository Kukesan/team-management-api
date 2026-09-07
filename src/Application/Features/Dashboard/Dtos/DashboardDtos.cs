using Application.Common.Models;
using Domain.Enums;

namespace Application.Features.Dashboard.Dtos;

public class DashboardSummaryDto
{
    public DateOnly WeekStartDate { get; set; }
    public int TotalSubmitted { get; set; }

    /// <summary>
    /// TotalSubmitted / active-user-count * 100. "Expected" reports = one per active
    /// user for the week; there's no per-project reporting requirement, so this is a
    /// simple headline number rather than a precise obligation count.
    /// </summary>
    public double ComplianceRatePercent { get; set; }
    public int NeedsCorrectionCount { get; set; }
    public int OpenBlockersCount { get; set; }
}

public class TasksTrendPointDto
{
    public DateOnly WeekStartDate { get; set; }
    public int CompletedTaskCount { get; set; }
}

public class MemberWeekReportDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
}

public class StatusByMemberDto
{
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }

    /// <summary>Empty means this member has no report at all for the week — a compliance gap.</summary>
    public List<MemberWeekReportDto> Reports { get; set; } = new();
}

public class WorkloadByProjectDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public int TaskCount { get; set; }
}

public class TimeByTaskTypeDto
{
    public HoursTaskType TaskType { get; set; }
    public decimal TotalHours { get; set; }
}

public enum ActivityType
{
    Submission,
    Review
}

public class ActivityFeedItemDto
{
    public ActivityType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid ReportId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ActorFullName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public class DashboardWeekQuery
{
    public DateOnly? Week { get; set; }
}

public class TasksTrendQuery
{
    public Guid? UserId { get; set; }
    private int _weeks = 8;
    public int Weeks
    {
        get => _weeks;
        set => _weeks = value switch { < 1 => 1, > 52 => 52, _ => value };
    }
}

public class DateRangeQuery
{
    public DateOnly? WeekStartDate { get; set; }
    public DateOnly? WeekEndDate { get; set; }
    public Guid? UserId { get; set; }
}

public class ActivityFeedQuery : PaginationParameters
{
}
