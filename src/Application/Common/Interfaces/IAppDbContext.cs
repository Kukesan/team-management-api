using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

/// <summary>
/// Thin abstraction over AppDbContext so the Application layer can query/persist
/// without depending on the concrete Infrastructure/EF implementation. Deliberately
/// not a repository-per-entity — for a domain this size that would just add
/// indirection around what's already a well-tested EF Core API, and this still
/// keeps services unit-testable against a real DbContext (e.g. via the SQLite or
/// InMemory provider) rather than the real Postgres database.
/// </summary>
public interface IAppDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectAssignment> ProjectAssignments { get; }
    DbSet<Report> Reports { get; }
    DbSet<ReportVersion> ReportVersions { get; }
    DbSet<ReportTaskItem> ReportTaskItems { get; }
    DbSet<ReportNextWeekTask> ReportNextWeekTasks { get; }
    DbSet<ReportBlocker> ReportBlockers { get; }
    DbSet<ReportAchievement> ReportAchievements { get; }
    DbSet<ReportHoursBreakdown> ReportHoursBreakdowns { get; }
    DbSet<ReportReview> ReportReviews { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
