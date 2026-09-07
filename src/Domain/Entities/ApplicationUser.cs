using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

/// <summary>
/// Identity owns credentials/roles; role membership is stored via AspNetUserRoles,
/// not a column here, so a user can be queried/authorized through the standard
/// RoleManager/UserManager APIs and [Authorize(Roles=...)].
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Project> CreatedProjects { get; set; } = new List<Project>();
    public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
    public ICollection<ReportReview> ReviewsGiven { get; set; } = new List<ReportReview>();
}
