using Domain.Enums;

namespace Domain.Entities;

public class ReportHoursBreakdown
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public HoursTaskType TaskType { get; set; }
    public decimal Hours { get; set; }
}
