using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Report is the durable identity of a week's report (one row per user/project/week).
/// Its child collections (TaskItems, Blockers, Achievements, HoursBreakdown) hold the
/// CURRENT/working content — what's shown and edited while the report is Draft or
/// NeedsCorrection, and what dashboard aggregation queries read directly.
///
/// On every Submit, the service layer serializes that current content into a new
/// immutable ReportVersion (content_snapshot JSONB) and bumps CurrentVersionNumber.
/// The working rows are NOT deleted on submit — if a manager requests changes, the
/// team member edits these same rows again and re-submits, producing version 2, 3, etc.
/// This gives full history (via ReportVersion snapshots) without duplicating the
/// relational child tables per version.
/// </summary>
public class Report
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public int CurrentVersionNumber { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReportTaskItem> TaskItems { get; set; } = new List<ReportTaskItem>();
    public ICollection<ReportBlocker> Blockers { get; set; } = new List<ReportBlocker>();
    public ICollection<ReportAchievement> Achievements { get; set; } = new List<ReportAchievement>();
    public ICollection<ReportHoursBreakdown> HoursBreakdown { get; set; } = new List<ReportHoursBreakdown>();

    public ICollection<ReportVersion> Versions { get; set; } = new List<ReportVersion>();
    public ICollection<ReportReview> Reviews { get; set; } = new List<ReportReview>();
}
