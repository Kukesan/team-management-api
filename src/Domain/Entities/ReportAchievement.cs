namespace Domain.Entities;

public class ReportAchievement
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public bool IsKeyAchievement { get; set; }
}
