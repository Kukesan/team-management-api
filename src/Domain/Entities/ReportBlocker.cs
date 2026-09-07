namespace Domain.Entities;

public class ReportBlocker
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public bool IsKeyIssue { get; set; }
    public bool IsResolved { get; set; }
}
