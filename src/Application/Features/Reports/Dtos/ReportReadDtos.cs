using Domain.Enums;

namespace Application.Features.Reports.Dtos;

public class ReportListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public ReportStatus Status { get; set; }
    public int CurrentVersionNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TaskItemDto
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public int PlannedPercent { get; set; }
    public int ActualPercent { get; set; }
    public TaskItemStatus Status { get; set; }
    public decimal TimePlannedHours { get; set; }
    public decimal TimeSpentHours { get; set; }
    public string? Output { get; set; }
}

public class BlockerDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsKeyIssue { get; set; }
    public bool IsResolved { get; set; }
}

public class AchievementDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsKeyAchievement { get; set; }
}

public class HoursBreakdownDto
{
    public Guid Id { get; set; }
    public HoursTaskType TaskType { get; set; }
    public decimal Hours { get; set; }
}

public class ReportReviewDto
{
    public Guid Id { get; set; }
    public int ReportVersionNumber { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerFullName { get; set; } = string.Empty;
    public ReviewAction Action { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReportDetailDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public ReportStatus Status { get; set; }
    public int CurrentVersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<TaskItemDto> TaskItems { get; set; } = new();
    public List<BlockerDto> Blockers { get; set; } = new();
    public List<AchievementDto> Achievements { get; set; } = new();
    public List<HoursBreakdownDto> HoursBreakdown { get; set; } = new();
    public List<ReportReviewDto> Reviews { get; set; } = new();
}

public class ReportVersionSummaryDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class ReportVersionDetailDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<TaskItemDto> TaskItems { get; set; } = new();
    public List<BlockerDto> Blockers { get; set; } = new();
    public List<AchievementDto> Achievements { get; set; } = new();
    public List<HoursBreakdownDto> HoursBreakdown { get; set; } = new();
}

/// <summary>Full copy of a report's content, serialized into ReportVersion.ContentSnapshot at submit time.</summary>
public class ReportContentSnapshotDto
{
    public List<TaskItemDto> TaskItems { get; set; } = new();
    public List<BlockerDto> Blockers { get; set; } = new();
    public List<AchievementDto> Achievements { get; set; } = new();
    public List<HoursBreakdownDto> HoursBreakdown { get; set; } = new();
}
