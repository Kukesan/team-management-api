using Domain.Enums;

namespace Domain.Entities;

public class ReportTaskItem
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string TaskName { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public int PlannedPercent { get; set; }
    public int ActualPercent { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;
    public decimal TimePlannedHours { get; set; }
    public decimal TimeSpentHours { get; set; }
    public string? Output { get; set; }
}
