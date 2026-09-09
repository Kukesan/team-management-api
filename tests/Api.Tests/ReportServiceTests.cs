using Api.Tests.TestHelpers;
using Application.Common.Exceptions;
using Application.Features.Reports;
using Application.Features.Reports.Dtos;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Role-based access control and review-workflow coverage for ReportService --
/// the core of the assignment's required RBAC guarantee ("a team member must never
/// be able to access another team member's report data") and the mandated
/// submit -> request changes -> edit -> resubmit -> approve cycle.
/// </summary>
public class ReportServiceTests
{
    private static ApplicationUser MakeUser(string fullName) => new()
    {
        Id = Guid.NewGuid(),
        FullName = fullName,
        Email = $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.local",
        UserName = $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.local",
        IsActive = true
    };

    private static Project MakeProject(Guid createdById) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Project",
        IsActive = true,
        CreatedById = createdById
    };

    /// <summary>Seeds two team members, a manager, and a project; returns a ready-to-use ReportService.</summary>
    private static (ReportService Service, ApplicationUser Owner, ApplicationUser OtherMember, ApplicationUser Manager, Project Project, Infrastructure.Persistence.AppDbContext Db)
        SeedContext()
    {
        var db = InMemoryAppDbContextFactory.Create();

        var owner = MakeUser("Alice Owner");
        var otherMember = MakeUser("Bob Other");
        var manager = MakeUser("Carol Manager");
        var project = MakeProject(manager.Id);

        db.Users.AddRange(owner, otherMember, manager);
        db.Projects.Add(project);
        db.SaveChanges();

        return (new ReportService(db), owner, otherMember, manager, project, db);
    }

    private static async Task<ReportDetailDto> CreateDraftWithOneTaskAsync(ReportService service, Guid ownerId, Guid projectId)
    {
        var created = await service.CreateAsync(ownerId, new CreateReportRequest
        {
            ProjectId = projectId,
            WeekStartDate = new DateOnly(2026, 1, 5),
            WeekEndDate = new DateOnly(2026, 1, 11)
        });

        return await service.UpdateAsync(ownerId, created.Id, new UpdateReportRequest
        {
            TaskItems = new List<TaskItemRequest>
            {
                new()
                {
                    TaskName = "Ship feature",
                    Priority = TaskPriority.High,
                    PlannedPercent = 100,
                    ActualPercent = 50,
                    Status = TaskItemStatus.InProgress,
                    TimePlannedHours = 8,
                    TimeSpentHours = 4
                }
            }
        });
    }

    [Fact]
    public async Task GetByIdAsync_TeamMember_CannotReadAnotherMembersReport()
    {
        var (service, owner, otherMember, _, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetByIdAsync(otherMember.Id, new List<string> { Roles.TeamMember }, report.Id));
    }

    [Fact]
    public async Task GetByIdAsync_Owner_CanReadOwnDraftReport()
    {
        var (service, owner, _, _, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);

        var result = await service.GetByIdAsync(owner.Id, new List<string> { Roles.TeamMember }, report.Id);

        Assert.Equal(report.Id, result.Id);
        Assert.Equal(ReportStatus.Draft, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_Manager_CannotReadAnotherUsersDraftReport()
    {
        // Spec Sec3: "Draft -- only visible to them". A manager's oversight access does
        // not extend to a still-private draft belonging to someone else.
        var (service, owner, _, manager, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetByIdAsync(manager.Id, new List<string> { Roles.Manager }, report.Id));
    }

    [Fact]
    public async Task GetByIdAsync_Manager_CanReadAnotherUsersSubmittedReport()
    {
        var (service, owner, _, manager, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);
        await service.SubmitAsync(owner.Id, report.Id);

        var result = await service.GetByIdAsync(manager.Id, new List<string> { Roles.Manager }, report.Id);

        Assert.Equal(ReportStatus.Submitted, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_TeamMember_CannotEditAnotherMembersReport()
    {
        var (service, owner, otherMember, _, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(otherMember.Id, report.Id, new UpdateReportRequest
            {
                TaskItems = new List<TaskItemRequest>
                {
                    new() { TaskName = "Hijacked", Priority = TaskPriority.Low, Status = TaskItemStatus.NotStarted }
                }
            }));
    }

    [Fact]
    public async Task SubmitAsync_TeamMember_CannotSubmitAnotherMembersReport()
    {
        var (service, owner, otherMember, _, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitAsync(otherMember.Id, report.Id));
    }

    [Fact]
    public async Task GetAllForManagerAsync_ExcludesDraftReports_EvenWhenExplicitlyFiltered()
    {
        var (service, owner, _, _, project, _) = SeedContext();
        await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id); // stays Draft

        var allResult = await service.GetAllForManagerAsync(new ManagerReportsQueryParameters());
        Assert.Empty(allResult.Items);

        var explicitDraftFilter = await service.GetAllForManagerAsync(
            new ManagerReportsQueryParameters { Status = ReportStatus.Draft });
        Assert.Empty(explicitDraftFilter.Items);
    }

    [Fact]
    public async Task ReviewAsync_OnNonSubmittedReport_ThrowsConflict()
    {
        var (service, owner, _, manager, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id); // still Draft

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ReviewAsync(manager.Id, report.Id, new ReviewRequest { Action = "Approved" }));
    }

    [Fact]
    public async Task SubmitAsync_WithNoTaskItems_ThrowsValidation()
    {
        var (service, owner, _, _, project, _) = SeedContext();
        var created = await service.CreateAsync(owner.Id, new CreateReportRequest
        {
            ProjectId = project.Id,
            WeekStartDate = new DateOnly(2026, 1, 5),
            WeekEndDate = new DateOnly(2026, 1, 11)
        });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(owner.Id, created.Id));
    }

    [Fact]
    public async Task FullReviewCycle_SubmitRequestChangesEditResubmitApprove_UpdatesStatusAtEachStep()
    {
        var (service, owner, _, manager, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);
        Assert.Equal(ReportStatus.Draft, report.Status);

        var submitted = await service.SubmitAsync(owner.Id, report.Id);
        Assert.Equal(ReportStatus.Submitted, submitted.Status);
        Assert.Equal(1, submitted.CurrentVersionNumber);

        var sentBack = await service.ReviewAsync(manager.Id, report.Id, new ReviewRequest
        {
            Action = "RequestedChanges",
            Comment = "Please add more detail to the output field."
        });
        Assert.Equal(ReportStatus.NeedsCorrection, sentBack.Status);
        Assert.Contains(sentBack.Reviews, r => r.Action == ReviewAction.RequestedChanges);

        var edited = await service.UpdateAsync(owner.Id, report.Id, new UpdateReportRequest
        {
            TaskItems = new List<TaskItemRequest>
            {
                new()
                {
                    TaskName = "Ship feature", Priority = TaskPriority.High, PlannedPercent = 100,
                    ActualPercent = 100, Status = TaskItemStatus.Completed,
                    TimePlannedHours = 8, TimeSpentHours = 8, Output = "Deployed to prod"
                }
            }
        });
        Assert.Equal(ReportStatus.NeedsCorrection, edited.Status); // editing alone doesn't resubmit

        var resubmitted = await service.SubmitAsync(owner.Id, report.Id);
        Assert.Equal(ReportStatus.Submitted, resubmitted.Status);
        Assert.Equal(2, resubmitted.CurrentVersionNumber);

        var approved = await service.ReviewAsync(manager.Id, report.Id, new ReviewRequest { Action = "Approved" });
        Assert.Equal(ReportStatus.Approved, approved.Status);
        Assert.Equal(2, approved.Reviews.Count); // both review actions retained

        // Version history: both submissions produced a distinct, still-viewable version.
        var versions = await service.GetVersionsAsync(manager.Id, new List<string> { Roles.Manager }, report.Id);
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task UpdateAsync_OnApprovedReport_ThrowsConflict()
    {
        var (service, owner, _, manager, project, _) = SeedContext();
        var report = await CreateDraftWithOneTaskAsync(service, owner.Id, project.Id);
        await service.SubmitAsync(owner.Id, report.Id);
        await service.ReviewAsync(manager.Id, report.Id, new ReviewRequest { Action = "Approved" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(owner.Id, report.Id, new UpdateReportRequest
            {
                TaskItems = new List<TaskItemRequest>
                {
                    new() { TaskName = "Late edit", Priority = TaskPriority.Low, Status = TaskItemStatus.NotStarted }
                }
            }));
    }
}
