using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Created only by the manager review workflow (POST /api/reports/{id}/review).
/// Recording ReportVersionId (not just ReportId) pins the review to the exact
/// version that was judged, which is what the "review comment history" view needs
/// when a report has gone through multiple correction cycles.
/// </summary>
public class ReportReview
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public Guid ReportVersionId { get; set; }
    public ReportVersion ReportVersion { get; set; } = null!;

    public Guid ReviewerId { get; set; }
    public ApplicationUser Reviewer { get; set; } = null!;

    public ReviewAction Action { get; set; }
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
