using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportVersion> ReportVersions => Set<ReportVersion>();
    public DbSet<ReportTaskItem> ReportTaskItems => Set<ReportTaskItem>();
    public DbSet<ReportNextWeekTask> ReportNextWeekTasks => Set<ReportNextWeekTask>();
    public DbSet<ReportBlocker> ReportBlockers => Set<ReportBlocker>();
    public DbSet<ReportAchievement> ReportAchievements => Set<ReportAchievement>();
    public DbSet<ReportHoursBreakdown> ReportHoursBreakdowns => Set<ReportHoursBreakdown>();
    public DbSet<ReportReview> ReportReviews => Set<ReportReview>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
