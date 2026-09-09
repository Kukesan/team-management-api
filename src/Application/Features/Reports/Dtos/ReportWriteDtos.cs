using Domain.Enums;

namespace Application.Features.Reports.Dtos;

public class CreateReportRequest
{
    public Guid ProjectId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
}

/// <summary>
/// Full replace on every PUT: the request carries the complete current state of
/// each child collection, and the service diffs it against the database rather
/// than accepting per-item patches. Simpler and matches how a report-editing form
/// naturally saves (submit the whole draft each time).
/// </summary>
public class UpdateReportRequest
{
    public List<TaskItemRequest> TaskItems { get; set; } = new();
    public List<NextWeekTaskRequest> NextWeekTasks { get; set; } = new();
    public List<BlockerRequest> Blockers { get; set; } = new();
    public List<AchievementRequest> Achievements { get; set; } = new();
    public List<HoursBreakdownRequest> HoursBreakdown { get; set; } = new();

    /// <summary>Optional free-text notes or links.</summary>
    public string? Notes { get; set; }
}

public class TaskItemRequest
{
    public string TaskName { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public int PlannedPercent { get; set; }
    public int ActualPercent { get; set; }
    public TaskItemStatus Status { get; set; }
    public decimal TimePlannedHours { get; set; }
    public decimal TimeSpentHours { get; set; }
    public string? Output { get; set; }
}

public class NextWeekTaskRequest
{
    public string Description { get; set; } = string.Empty;
}

public class BlockerRequest
{
    public string Description { get; set; } = string.Empty;
    public bool IsKeyIssue { get; set; }
    public bool IsResolved { get; set; }
}

public class AchievementRequest
{
    public string Description { get; set; } = string.Empty;
    public bool IsKeyAchievement { get; set; }
}

public class HoursBreakdownRequest
{
    public HoursTaskType TaskType { get; set; }
    public decimal Hours { get; set; }
}

public class ReviewRequest
{
    /// <summary>"Approve" or "RequestChanges" — validated against the allowed set by ReviewRequestValidator.</summary>
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
