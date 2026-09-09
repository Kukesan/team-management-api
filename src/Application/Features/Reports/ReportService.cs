using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Reports.Dtos;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports;

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;

    public ReportService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ReportDetailDto> CreateAsync(Guid userId, CreateReportRequest request, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!projectExists)
        {
            throw new NotFoundException("Project", request.ProjectId);
        }

        // var duplicate = await _db.Reports.AnyAsync(
        //     r => r.UserId == userId && r.ProjectId == request.ProjectId && r.WeekStartDate == request.WeekStartDate, ct);
        // if (duplicate)
        // {
        //     throw new ConflictException("A report for this project and week already exists.");
        // }

        var report = new Report
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = request.ProjectId,
            WeekStartDate = request.WeekStartDate,
            WeekEndDate = request.WeekEndDate,
            Status = ReportStatus.Draft,
            CurrentVersionNumber = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);

        var loaded = await LoadReportWithChildrenAsync(report.Id, ct);
        return MapDetail(loaded);
    }

    public async Task<ReportDetailDto> UpdateAsync(Guid userId, Guid reportId, UpdateReportRequest request, CancellationToken ct = default)
    {
        var report = await LoadReportWithChildrenAsync(reportId, ct);

        if (report.UserId != userId)
        {
            throw new ForbiddenException("You can only edit your own reports.");
        }

        if (report.Status is not (ReportStatus.Draft or ReportStatus.NeedsCorrection))
        {
            throw new ConflictException($"Report cannot be edited while in '{report.Status}' status.");
        }

        _db.ReportTaskItems.RemoveRange(report.TaskItems);
        _db.ReportNextWeekTasks.RemoveRange(report.NextWeekTasks);
        _db.ReportBlockers.RemoveRange(report.Blockers);
        _db.ReportAchievements.RemoveRange(report.Achievements);
        _db.ReportHoursBreakdowns.RemoveRange(report.HoursBreakdown);

        foreach (var item in request.TaskItems)
        {
            _db.ReportTaskItems.Add(new ReportTaskItem
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                TaskName = item.TaskName,
                Priority = item.Priority,
                PlannedPercent = item.PlannedPercent,
                ActualPercent = item.ActualPercent,
                Status = item.Status,
                TimePlannedHours = item.TimePlannedHours,
                TimeSpentHours = item.TimeSpentHours,
                Output = item.Output
            });
        }

        foreach (var nextWeekTask in request.NextWeekTasks)
        {
            _db.ReportNextWeekTasks.Add(new ReportNextWeekTask
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                Description = nextWeekTask.Description
            });
        }

        foreach (var blocker in request.Blockers)
        {
            _db.ReportBlockers.Add(new ReportBlocker
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                Description = blocker.Description,
                IsKeyIssue = blocker.IsKeyIssue,
                IsResolved = blocker.IsResolved
            });
        }

        foreach (var achievement in request.Achievements)
        {
            _db.ReportAchievements.Add(new ReportAchievement
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                Description = achievement.Description,
                IsKeyAchievement = achievement.IsKeyAchievement
            });
        }

        foreach (var hours in request.HoursBreakdown)
        {
            _db.ReportHoursBreakdowns.Add(new ReportHoursBreakdown
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                TaskType = hours.TaskType,
                Hours = hours.Hours
            });
        }

        report.Notes = request.Notes;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var reloaded = await LoadReportWithChildrenAsync(report.Id, ct);
        return MapDetail(reloaded);
    }

    public async Task<ReportDetailDto> SubmitAsync(Guid userId, Guid reportId, CancellationToken ct = default)
    {
        var report = await LoadReportWithChildrenAsync(reportId, ct);

        if (report.UserId != userId)
        {
            throw new ForbiddenException("You can only submit your own reports.");
        }

        if (report.Status is not (ReportStatus.Draft or ReportStatus.NeedsCorrection))
        {
            throw new ConflictException($"Report cannot be submitted while in '{report.Status}' status.");
        }

        if (report.TaskItems.Count == 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["TaskItems"] = new[] { "At least one task item is required before submitting." }
            });
        }

        var snapshot = new ReportContentSnapshotDto
        {
            TaskItems = report.TaskItems.Select(MapTaskItem).ToList(),
            NextWeekTasks = report.NextWeekTasks.Select(MapNextWeekTask).ToList(),
            Blockers = report.Blockers.Select(MapBlocker).ToList(),
            Achievements = report.Achievements.Select(MapAchievement).ToList(),
            HoursBreakdown = report.HoursBreakdown.Select(MapHours).ToList(),
            Notes = report.Notes
        };

        var version = new ReportVersion
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            VersionNumber = report.CurrentVersionNumber + 1,
            ContentSnapshot = JsonSerializer.Serialize(snapshot),
            SubmittedAt = DateTime.UtcNow
        };

        _db.ReportVersions.Add(version);
        report.CurrentVersionNumber = version.VersionNumber;
        report.Status = ReportStatus.Submitted;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var reloaded = await LoadReportWithChildrenAsync(report.Id, ct);
        return MapDetail(reloaded);
    }

    public async Task<PagedResult<ReportListItemDto>> GetMineAsync(Guid userId, MyReportsQueryParameters query, CancellationToken ct = default)
    {
        var reports = _db.Reports.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Project)
            .Where(r => r.UserId == userId);

        if (query.ProjectId.HasValue)
        {
            reports = reports.Where(r => r.ProjectId == query.ProjectId.Value);
        }

        if (query.Status.HasValue)
        {
            reports = reports.Where(r => r.Status == query.Status.Value);
        }

        if (query.WeekStartDate.HasValue)
        {
            reports = reports.Where(r => r.WeekStartDate == query.WeekStartDate.Value);
        }

        return await PaginateAsync(reports, query, ct);
    }

    public async Task<ReportDetailDto> GetByIdAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, CancellationToken ct = default)
    {
        var report = await LoadReportWithChildrenAsync(reportId, ct);
        EnsureOwnerOrManager(report, requestingUserId, requestingUserRoles);
        return MapDetail(report);
    }

    public async Task<IReadOnlyList<ReportVersionSummaryDto>> GetVersionsAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, CancellationToken ct = default)
    {
        var report = await _db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException("Report", reportId);
        EnsureOwnerOrManager(report, requestingUserId, requestingUserRoles);

        return await _db.ReportVersions.AsNoTracking()
            .Where(v => v.ReportId == reportId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ReportVersionSummaryDto { Id = v.Id, VersionNumber = v.VersionNumber, SubmittedAt = v.SubmittedAt })
            .ToListAsync(ct);
    }

    public async Task<ReportVersionDetailDto> GetVersionAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, Guid versionId, CancellationToken ct = default)
    {
        var report = await _db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException("Report", reportId);
        EnsureOwnerOrManager(report, requestingUserId, requestingUserRoles);

        var version = await _db.ReportVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.ReportId == reportId, ct)
            ?? throw new NotFoundException("ReportVersion", versionId);

        var snapshot = JsonSerializer.Deserialize<ReportContentSnapshotDto>(version.ContentSnapshot) ?? new ReportContentSnapshotDto();

        return new ReportVersionDetailDto
        {
            Id = version.Id,
            VersionNumber = version.VersionNumber,
            SubmittedAt = version.SubmittedAt,
            TaskItems = snapshot.TaskItems,
            NextWeekTasks = snapshot.NextWeekTasks,
            Blockers = snapshot.Blockers,
            Achievements = snapshot.Achievements,
            HoursBreakdown = snapshot.HoursBreakdown,
            Notes = snapshot.Notes
        };
    }

    public async Task<PagedResult<ReportListItemDto>> GetAllForManagerAsync(ManagerReportsQueryParameters query, CancellationToken ct = default)
    {
        var reports = _db.Reports.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Project)
            // "Draft — only visible to them" (spec §3): a draft never appears on the manager's
            // dashboard regardless of filters, even if a caller explicitly asks for Status=Draft.
            .Where(r => r.Status != ReportStatus.Draft)
            .AsQueryable();

        if (query.UserId.HasValue)
        {
            reports = reports.Where(r => r.UserId == query.UserId.Value);
        }

        if (query.ProjectId.HasValue)
        {
            reports = reports.Where(r => r.ProjectId == query.ProjectId.Value);
        }

        if (query.Status.HasValue && query.Status.Value != ReportStatus.Draft)
        {
            reports = reports.Where(r => r.Status == query.Status.Value);
        }

        if (query.DateFrom.HasValue)
        {
            reports = reports.Where(r => r.WeekStartDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            reports = reports.Where(r => r.WeekEndDate <= query.DateTo.Value);
        }

        return await PaginateAsync(reports, query, ct);
    }

    public async Task<ReportDetailDto> ReviewAsync(Guid reviewerId, Guid reportId, ReviewRequest request, CancellationToken ct = default)
    {
        var report = await LoadReportWithChildrenAsync(reportId, ct);

        if (report.Status != ReportStatus.Submitted)
        {
            throw new ConflictException($"Report is not awaiting review (current status: '{report.Status}').");
        }

        var latestVersion = await _db.ReportVersions.AsNoTracking()
            .Where(v => v.ReportId == reportId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Report {reportId} is Submitted but has no ReportVersion.");

        var action = request.Action == "Approved" ? ReviewAction.Approved : ReviewAction.RequestedChanges;

        // Status/comment are the ONLY things this method touches — TaskItems/Blockers/
        // Achievements/HoursBreakdown are never written here, by design.
        report.Status = action == ReviewAction.Approved ? ReportStatus.Approved : ReportStatus.NeedsCorrection;
        report.UpdatedAt = DateTime.UtcNow;

        _db.ReportReviews.Add(new ReportReview
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            ReportVersionId = latestVersion.Id,
            ReviewerId = reviewerId,
            Action = action,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        var reloaded = await LoadReportWithChildrenAsync(report.Id, ct);
        return MapDetail(reloaded);
    }

    private static void EnsureOwnerOrManager(Report report, Guid requestingUserId, IList<string> requestingUserRoles)
    {
        var isOwner = report.UserId == requestingUserId;
        var isManagerOrAdmin = requestingUserRoles.Intersect(Roles.ManagerOrAdmin).Any();

        if (!isOwner && !isManagerOrAdmin)
        {
            throw new ForbiddenException("You do not have access to this report.");
        }

        // "Draft — only visible to them" (spec §3): a manager's oversight access does not
        // extend to another user's still-private draft. Only the owner may see it pre-submission.
        if (!isOwner && report.Status == ReportStatus.Draft)
        {
            throw new ForbiddenException("This report is still a draft and has not been submitted for review.");
        }
    }

    private async Task<Report> LoadReportWithChildrenAsync(Guid reportId, CancellationToken ct)
    {
        return await _db.Reports
            .Include(r => r.User)
            .Include(r => r.Project)
            .Include(r => r.TaskItems)
            .Include(r => r.NextWeekTasks)
            .Include(r => r.Blockers)
            .Include(r => r.Achievements)
            .Include(r => r.HoursBreakdown)
            .Include(r => r.Reviews).ThenInclude(rv => rv.Reviewer)
            .Include(r => r.Reviews).ThenInclude(rv => rv.ReportVersion)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException("Report", reportId);
    }

    private static async Task<PagedResult<ReportListItemDto>> PaginateAsync(IQueryable<Report> query, PaginationParameters pagination, CancellationToken ct)
    {
        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "status" => pagination.SortDescending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "updatedat" => pagination.SortDescending ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt),
            _ => pagination.SortDescending ? query.OrderByDescending(r => r.WeekStartDate) : query.OrderBy(r => r.WeekStartDate)
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(r => new ReportListItemDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = r.User.FullName,
                ProjectId = r.ProjectId,
                ProjectName = r.Project.Name,
                WeekStartDate = r.WeekStartDate,
                WeekEndDate = r.WeekEndDate,
                Status = r.Status,
                CurrentVersionNumber = r.CurrentVersionNumber,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ReportListItemDto>
        {
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        };
    }

    private static ReportDetailDto MapDetail(Report report) => new()
    {
        Id = report.Id,
        UserId = report.UserId,
        UserFullName = report.User.FullName,
        ProjectId = report.ProjectId,
        ProjectName = report.Project.Name,
        WeekStartDate = report.WeekStartDate,
        WeekEndDate = report.WeekEndDate,
        Status = report.Status,
        CurrentVersionNumber = report.CurrentVersionNumber,
        CreatedAt = report.CreatedAt,
        UpdatedAt = report.UpdatedAt,
        TaskItems = report.TaskItems.Select(MapTaskItem).ToList(),
        NextWeekTasks = report.NextWeekTasks.Select(MapNextWeekTask).ToList(),
        Blockers = report.Blockers.Select(MapBlocker).ToList(),
        Achievements = report.Achievements.Select(MapAchievement).ToList(),
        HoursBreakdown = report.HoursBreakdown.Select(MapHours).ToList(),
        Notes = report.Notes,
        Reviews = report.Reviews
            .OrderByDescending(rv => rv.CreatedAt)
            .Select(rv => new ReportReviewDto
            {
                Id = rv.Id,
                ReportVersionNumber = rv.ReportVersion.VersionNumber,
                ReviewerId = rv.ReviewerId,
                ReviewerFullName = rv.Reviewer.FullName,
                Action = rv.Action,
                Comment = rv.Comment,
                CreatedAt = rv.CreatedAt
            })
            .ToList()
    };

    private static TaskItemDto MapTaskItem(ReportTaskItem t) => new()
    {
        Id = t.Id,
        TaskName = t.TaskName,
        Priority = t.Priority,
        PlannedPercent = t.PlannedPercent,
        ActualPercent = t.ActualPercent,
        Status = t.Status,
        TimePlannedHours = t.TimePlannedHours,
        TimeSpentHours = t.TimeSpentHours,
        Output = t.Output
    };

    private static NextWeekTaskDto MapNextWeekTask(ReportNextWeekTask t) => new()
    {
        Id = t.Id,
        Description = t.Description
    };

    private static BlockerDto MapBlocker(ReportBlocker b) => new()
    {
        Id = b.Id,
        Description = b.Description,
        IsKeyIssue = b.IsKeyIssue,
        IsResolved = b.IsResolved
    };

    private static AchievementDto MapAchievement(ReportAchievement a) => new()
    {
        Id = a.Id,
        Description = a.Description,
        IsKeyAchievement = a.IsKeyAchievement
    };

    private static HoursBreakdownDto MapHours(ReportHoursBreakdown h) => new()
    {
        Id = h.Id,
        TaskType = h.TaskType,
        Hours = h.Hours
    };
}
