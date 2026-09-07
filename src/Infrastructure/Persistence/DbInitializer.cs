using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeding: roles always, a bootstrap Admin only in Development
/// (so there's a way into the system before any real admin exists) using
/// Seed:AdminEmail / Seed:AdminPassword from configuration, falling back to a
/// documented dev-only default.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    public static async Task SeedDevAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@teammanagement.local";
        var adminPassword = configuration["Seed:AdminPassword"] ?? "ChangeMe123!";

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "System Administrator",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
            logger.LogWarning(
                "Seeded a development admin account ({Email}). Change its password or remove it before deploying anywhere real.",
                adminEmail);
        }
        else
        {
            logger.LogError(
                "Failed to seed development admin account: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Starter data: 4 team members, 2 managers, 4 projects, plus assignments — enough
    /// for the Reports workflow and, combined with SeedReportsAsync, for the Dashboard
    /// aggregations to show something meaningful.
    /// </summary>
    public static async Task SeedDemoUsersAndProjectsAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ILogger logger)
    {
        // Self-healing: also called on every startup for existing rows, so demo accounts
        // toggled while poking at the API (deactivated, reassigned a role) reset to their
        // seeded baseline instead of staying broken for the next test run.
        async Task<ApplicationUser> EnsureUserAsync(string email, string fullName, string role)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                var changed = false;
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    changed = true;
                }
                if (changed)
                {
                    await userManager.UpdateAsync(existing);
                }

                var currentRoles = await userManager.GetRolesAsync(existing);
                if (!currentRoles.Contains(role))
                {
                    if (currentRoles.Count > 0)
                    {
                        await userManager.RemoveFromRolesAsync(existing, currentRoles);
                    }
                    await userManager.AddToRoleAsync(existing, role);
                }

                return existing;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, "Password123");
            if (!result.Succeeded)
            {
                logger.LogError("Failed to seed demo user {Email}: {Errors}", email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                throw new InvalidOperationException($"Failed to seed demo user {email}");
            }

            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        var alice = await EnsureUserAsync("alice@teammanagement.local", "Alice TeamMember", Roles.TeamMember);
        var bob = await EnsureUserAsync("bob@teammanagement.local", "Bob TeamMember", Roles.TeamMember);
        var dave = await EnsureUserAsync("dave@teammanagement.local", "Dave TeamMember", Roles.TeamMember);
        var erin = await EnsureUserAsync("erin@teammanagement.local", "Erin TeamMember", Roles.TeamMember);
        var carol = await EnsureUserAsync("carol.manager@teammanagement.local", "Carol Manager", Roles.Manager);
        await EnsureUserAsync("frank.manager@teammanagement.local", "Frank Manager", Roles.Manager);

        // Self-healing per project name, same pattern as EnsureUserAsync: safe to call
        // on every startup even if some (or none, or extra unrelated) projects already
        // exist from earlier manual testing.
        async Task<Project> EnsureProjectAsync(string name, string description)
        {
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.Name == name);
            if (existing is not null)
            {
                return existing;
            }

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                IsActive = true,
                CreatedById = carol.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        }

        var websiteRevamp = await EnsureProjectAsync("Website Revamp", "Public site redesign");
        var mobileApp = await EnsureProjectAsync("Mobile App", "iOS/Android client");
        var apiPlatform = await EnsureProjectAsync("API Platform", "Public API v2");
        var internalTools = await EnsureProjectAsync("Internal Tools", "Ops dashboards and tooling");

        foreach (var project in new[] { websiteRevamp, mobileApp, apiPlatform, internalTools })
        {
            logger.LogInformation("Demo project available: {Name} = {Id}", project.Name, project.Id);
        }

        // Fixed, explicit member -> primary project mapping (rather than rotating an
        // index over a project list) so it stays deterministic across runs regardless
        // of what other projects already exist in the database from manual testing.
        var primaryAssignments = new (ApplicationUser Member, Project Primary, Project Secondary)[]
        {
            (alice, websiteRevamp, mobileApp),
            (bob, mobileApp, websiteRevamp),
            (dave, apiPlatform, internalTools),
            (erin, internalTools, apiPlatform)
        };

        foreach (var (member, primary, secondary) in primaryAssignments)
        {
            foreach (var project in new[] { primary, secondary })
            {
                var exists = await db.ProjectAssignments.AnyAsync(a => a.ProjectId == project.Id && a.UserId == member.Id);
                if (!exists)
                {
                    db.ProjectAssignments.Add(new ProjectAssignment
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = project.Id,
                        UserId = member.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Several weeks of reports across all four statuses, so the Dashboard aggregations
    /// (compliance rate, tasks trend, workload by project, time by task type, activity
    /// feed) have something meaningful to show. Idempotent per (user, project, week) —
    /// safe to call on every startup.
    /// </summary>
    public static async Task SeedReportsAsync(UserManager<ApplicationUser> userManager, AppDbContext db, ILogger logger)
    {
        var memberEmails = new[]
        {
            "alice@teammanagement.local", "bob@teammanagement.local",
            "dave@teammanagement.local", "erin@teammanagement.local"
        };

        var members = new List<ApplicationUser>();
        foreach (var email in memberEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                members.Add(user);
            }
        }

        var reviewer = await userManager.FindByEmailAsync("carol.manager@teammanagement.local");
        if (reviewer is null || members.Count == 0)
        {
            return;
        }

        // Same explicit member -> primary project mapping as SeedDemoUsersAndProjectsAsync
        // (looked up by name rather than shared in-memory, since this runs as an
        // independent step) — keeps report generation deterministic across runs.
        var projectsByName = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Name, p => p);
        var primaryProjectByEmail = new Dictionary<string, string>
        {
            ["alice@teammanagement.local"] = "Website Revamp",
            ["bob@teammanagement.local"] = "Mobile App",
            ["dave@teammanagement.local"] = "API Platform",
            ["erin@teammanagement.local"] = "Internal Tools"
        };

        var currentWeek = DashboardWeekAnchor();
        var createdAny = false;

        // 4 weeks of history, oldest first, cycling through all four statuses per member.
        var statusCycle = new[] { ReportStatus.Approved, ReportStatus.NeedsCorrection, ReportStatus.Submitted, ReportStatus.Draft };

        for (var weekIndex = 3; weekIndex >= 0; weekIndex--)
        {
            var weekStart = currentWeek.AddDays(-7 * weekIndex);
            var weekEnd = weekStart.AddDays(6);

            for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                var member = members[memberIndex];
                if (!primaryProjectByEmail.TryGetValue(member.Email!, out var projectName)
                    || !projectsByName.TryGetValue(projectName, out var project))
                {
                    continue;
                }

                var exists = await db.Reports.AnyAsync(r => r.UserId == member.Id && r.ProjectId == project.Id && r.WeekStartDate == weekStart);
                if (exists)
                {
                    continue;
                }

                var targetStatus = statusCycle[(memberIndex + weekIndex) % statusCycle.Length];
                CreateSeedReport(db, member, project, weekStart, weekEnd, targetStatus, reviewer);
                createdAny = true;
            }
        }

        if (createdAny)
        {
            await db.SaveChangesAsync();
            logger.LogWarning("Seeded 4 weeks of demo reports across Draft/Submitted/NeedsCorrection/Approved statuses.");
        }
    }

    private static DateOnly DashboardWeekAnchor()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var offsetFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-offsetFromMonday);
    }

    /// <summary>Npgsql requires DateTimeKind.Utc for timestamptz columns; DateOnly.ToDateTime leaves Kind=Unspecified.</summary>
    private static DateTime AsUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static void CreateSeedReport(
        AppDbContext db,
        ApplicationUser member,
        Project project,
        DateOnly weekStart,
        DateOnly weekEnd,
        ReportStatus targetStatus,
        ApplicationUser reviewer)
    {
        var report = new Report
        {
            Id = Guid.NewGuid(),
            UserId = member.Id,
            ProjectId = project.Id,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            Status = ReportStatus.Draft,
            CurrentVersionNumber = 0,
            CreatedAt = AsUtc(weekStart),
            UpdatedAt = AsUtc(weekStart)
        };

        var taskItems = new List<ReportTaskItem>
        {
            new()
            {
                Id = Guid.NewGuid(), ReportId = report.Id, TaskName = $"{project.Name} weekly deliverable",
                Priority = TaskPriority.High, PlannedPercent = 100,
                ActualPercent = targetStatus == ReportStatus.Draft ? 40 : 100,
                Status = targetStatus == ReportStatus.Draft ? TaskItemStatus.InProgress : TaskItemStatus.Completed,
                TimePlannedHours = 24, TimeSpentHours = targetStatus == ReportStatus.Draft ? 10 : 22,
                Output = targetStatus == ReportStatus.Draft ? null : "Delivered as planned"
            },
            new()
            {
                Id = Guid.NewGuid(), ReportId = report.Id, TaskName = "Code review and support",
                Priority = TaskPriority.Medium, PlannedPercent = 100, ActualPercent = 100,
                Status = TaskItemStatus.Completed, TimePlannedHours = 6, TimeSpentHours = 5,
                Output = "Reviewed teammates' PRs"
            }
        };

        var blockers = new List<ReportBlocker>
        {
            new()
            {
                Id = Guid.NewGuid(), ReportId = report.Id,
                Description = targetStatus == ReportStatus.NeedsCorrection
                    ? "Waiting on staging environment access"
                    : "Dependency on a third-party API rate limit",
                IsKeyIssue = true,
                IsResolved = targetStatus is ReportStatus.Approved or ReportStatus.Draft
            }
        };

        var achievements = new List<ReportAchievement>
        {
            new() { Id = Guid.NewGuid(), ReportId = report.Id, Description = $"Made progress on {project.Name}", IsKeyAchievement = true }
        };

        var hours = new List<ReportHoursBreakdown>
        {
            new() { Id = Guid.NewGuid(), ReportId = report.Id, TaskType = HoursTaskType.Development, Hours = 18 },
            new() { Id = Guid.NewGuid(), ReportId = report.Id, TaskType = HoursTaskType.Meetings, Hours = 4 },
            new() { Id = Guid.NewGuid(), ReportId = report.Id, TaskType = HoursTaskType.CodeReview, Hours = 5 }
        };

        db.Reports.Add(report);
        db.ReportTaskItems.AddRange(taskItems);
        db.ReportBlockers.AddRange(blockers);
        db.ReportAchievements.AddRange(achievements);
        db.ReportHoursBreakdowns.AddRange(hours);

        if (targetStatus == ReportStatus.Draft)
        {
            return;
        }

        // Every non-Draft seed report has gone through exactly one submission for
        // simplicity; NeedsCorrection reports look like they're mid-correction-cycle.
        var submittedAt = AsUtc(weekStart).AddDays(5);
        var version = new ReportVersion
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            VersionNumber = 1,
            ContentSnapshot = "{}",
            SubmittedAt = submittedAt
        };
        db.ReportVersions.Add(version);
        report.CurrentVersionNumber = 1;
        report.Status = ReportStatus.Submitted;
        report.UpdatedAt = submittedAt;

        if (targetStatus == ReportStatus.Submitted)
        {
            return;
        }

        var reviewedAt = submittedAt.AddDays(1);
        db.ReportReviews.Add(new ReportReview
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            ReportVersionId = version.Id,
            ReviewerId = reviewer.Id,
            Action = targetStatus == ReportStatus.Approved ? ReviewAction.Approved : ReviewAction.RequestedChanges,
            Comment = targetStatus == ReportStatus.Approved ? null : "Please double check the hours breakdown and resubmit.",
            CreatedAt = reviewedAt
        });

        report.Status = targetStatus;
        report.UpdatedAt = reviewedAt;
    }
}
