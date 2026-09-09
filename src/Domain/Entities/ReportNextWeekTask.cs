namespace Domain.Entities;

/// <summary>
/// "Tasks planned for next week" (spec Sec2) -- a simple planned-item list, same shape
/// as Blockers/Achievements, distinct from the task-level table used for Tasks Completed.
/// </summary>
public class ReportNextWeekTask
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
}
