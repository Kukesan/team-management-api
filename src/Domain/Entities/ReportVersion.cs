namespace Domain.Entities;

/// <summary>
/// Immutable snapshot created on every Submit. ContentSnapshot is a JSON blob
/// (mapped to jsonb in Postgres) holding a full copy of tasks/blockers/achievements/
/// hours at submission time, so history survives even though the live child tables
/// on Report keep changing across correction cycles.
/// </summary>
public class ReportVersion
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int VersionNumber { get; set; }

    /// <summary>Serialized ReportContentSnapshotDto (tasks, blockers, achievements, hours).</summary>
    public string ContentSnapshot { get; set; } = "{}";

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReportReview> Reviews { get; set; } = new List<ReportReview>();
}
