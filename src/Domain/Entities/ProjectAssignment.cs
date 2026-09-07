namespace Domain.Entities;

/// <summary>
/// Join entity; a (ProjectId, UserId) pair is unique — enforced via a composite
/// unique index in the DbContext configuration.
/// </summary>
public class ProjectAssignment
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
