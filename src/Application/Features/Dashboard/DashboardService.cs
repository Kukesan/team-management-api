using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Dashboard.Dtos;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db)
    {
        _db = db;
    }

    public static DateOnly CurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var offsetFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-offsetFromMonday);
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? week, CancellationToken ct = default)
    {
        var weekStart = week ?? CurrentWeekStart();

        var weekReports = _db.Reports.AsNoTracking().Where(r => r.WeekStartDate == weekStart);

        var totalSubmitted = await weekReports.CountAsync(r => r.CurrentVersionNumber >= 1, ct);
        var needsCorrectionCount = await weekReports.CountAsync(r => r.Status == ReportStatus.NeedsCorrection, ct);

        var openBlockersCount = await _db.ReportBlockers.AsNoTracking()
            .CountAsync(b => b.Report.WeekStartDate == weekStart && !b.IsResolved, ct);

        var activeUserCount = await _db.Users.AsNoTracking().CountAsync(u => u.IsActive, ct);
        var complianceRate = activeUserCount == 0 ? 0 : Math.Round(totalSubmitted * 100.0 / activeUserCount, 1);

        return new DashboardSummaryDto
        {
            WeekStartDate = weekStart,
            TotalSubmitted = totalSubmitted,
            ComplianceRatePercent = complianceRate,
            NeedsCorrectionCount = needsCorrectionCount,
            OpenBlockersCount = openBlockersCount
        };
    }

    public async Task<IReadOnlyList<TasksTrendPointDto>> GetTasksTrendAsync(TasksTrendQuery query, CancellationToken ct = default)
    {
        var currentWeek = CurrentWeekStart();
        var earliestWeek = currentWeek.AddDays(-7 * (query.Weeks - 1));

        var taskItems = _db.ReportTaskItems.AsNoTracking()
            .Where(t => t.Status == TaskItemStatus.Completed
                && t.Report.WeekStartDate >= earliestWeek
                && t.Report.WeekStartDate <= currentWeek);

        if (query.UserId.HasValue)
        {
            taskItems = taskItems.Where(t => t.Report.UserId == query.UserId.Value);
        }

        var counts = await taskItems
            .GroupBy(t => t.Report.WeekStartDate)
            .Select(g => new { WeekStartDate = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countsByWeek = counts.ToDictionary(c => c.WeekStartDate, c => c.Count);

        var result = new List<TasksTrendPointDto>();
        for (var week = earliestWeek; week <= currentWeek; week = week.AddDays(7))
        {
            result.Add(new TasksTrendPointDto
            {
                WeekStartDate = week,
                CompletedTaskCount = countsByWeek.GetValueOrDefault(week, 0)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<StatusByMemberDto>> GetStatusByMemberAsync(DateOnly? week, CancellationToken ct = default)
    {
        var weekStart = week ?? await _db.Reports.AsNoTracking()
            .OrderByDescending(r => r.WeekStartDate)
            .Select(r => (DateOnly?)r.WeekStartDate)
            .FirstOrDefaultAsync(ct) ?? CurrentWeekStart();

        var activeUsers = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.FullName })
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var reportsThisWeek = await _db.Reports.AsNoTracking()
            .Include(r => r.Project)
            .Where(r => r.WeekStartDate == weekStart)
            .ToListAsync(ct);

        var reportsByUser = reportsThisWeek.ToLookup(r => r.UserId);

        return activeUsers.Select(u => new StatusByMemberDto
        {
            UserId = u.Id,
            UserFullName = u.FullName,
            WeekStartDate = weekStart,
            Reports = reportsByUser[u.Id]
                .Select(r => new MemberWeekReportDto { ProjectId = r.ProjectId, ProjectName = r.Project.Name, Status = r.Status })
                .ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<WorkloadByProjectDto>> GetWorkloadByProjectAsync(DateRangeQuery query, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(query);

        var hoursByProject = await _db.ReportHoursBreakdowns.AsNoTracking()
            .Where(h => h.Report.WeekStartDate >= from && h.Report.WeekStartDate <= to
                && (!query.UserId.HasValue || h.Report.UserId == query.UserId.Value))
            .GroupBy(h => new { h.Report.ProjectId, h.Report.Project.Name })
            .Select(g => new { g.Key.ProjectId, g.Key.Name, TotalHours = g.Sum(h => h.Hours) })
            .ToListAsync(ct);

        var taskCountsByProject = await _db.ReportTaskItems.AsNoTracking()
            .Where(t => t.Report.WeekStartDate >= from && t.Report.WeekStartDate <= to
                && (!query.UserId.HasValue || t.Report.UserId == query.UserId.Value))
            .GroupBy(t => t.Report.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count, ct);

        return hoursByProject.Select(h => new WorkloadByProjectDto
        {
            ProjectId = h.ProjectId,
            ProjectName = h.Name,
            TotalHours = h.TotalHours,
            TaskCount = taskCountsByProject.GetValueOrDefault(h.ProjectId, 0)
        })
        .OrderByDescending(w => w.TotalHours)
        .ToList();
    }

    public async Task<IReadOnlyList<TimeByTaskTypeDto>> GetTimeByTaskTypeAsync(DateRangeQuery query, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(query);

        var result = await _db.ReportHoursBreakdowns.AsNoTracking()
            .Where(h => h.Report.WeekStartDate >= from && h.Report.WeekStartDate <= to
                && (!query.UserId.HasValue || h.Report.UserId == query.UserId.Value))
            .GroupBy(h => h.TaskType)
            .Select(g => new TimeByTaskTypeDto { TaskType = g.Key, TotalHours = g.Sum(h => h.Hours) })
            .OrderByDescending(t => t.TotalHours)
            .ToListAsync(ct);

        return result;
    }

    public async Task<PagedResult<ActivityFeedItemDto>> GetActivityFeedAsync(ActivityFeedQuery query, CancellationToken ct = default)
    {
        // A scoped DbContext isn't thread-safe, so these run sequentially rather than
        // via Task.WhenAll — fetch up to `take` from each source (bounded by page depth)
        // and merge in memory. Fine at this scale; would need a real UNION query (or a
        // dedicated activity-log table) if the feed needed to page arbitrarily deep.
        var take = query.Page * query.PageSize;

        var submissions = await _db.ReportVersions.AsNoTracking()
            .Include(v => v.Report).ThenInclude(r => r.Project)
            .Include(v => v.Report).ThenInclude(r => r.User)
            .OrderByDescending(v => v.SubmittedAt)
            .Take(take)
            .Select(v => new ActivityFeedItemDto
            {
                Type = ActivityType.Submission,
                Timestamp = v.SubmittedAt,
                ReportId = v.ReportId,
                ProjectId = v.Report.ProjectId,
                ProjectName = v.Report.Project.Name,
                ActorFullName = v.Report.User.FullName,
                Detail = $"Submitted version {v.VersionNumber}"
            })
            .ToListAsync(ct);

        var reviews = await _db.ReportReviews.AsNoTracking()
            .Include(r => r.Report).ThenInclude(rp => rp.Project)
            .Include(r => r.Reviewer)
            .Include(r => r.ReportVersion)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new ActivityFeedItemDto
            {
                Type = ActivityType.Review,
                Timestamp = r.CreatedAt,
                ReportId = r.ReportId,
                ProjectId = r.Report.ProjectId,
                ProjectName = r.Report.Project.Name,
                ActorFullName = r.Reviewer.FullName,
                Detail = r.Action == ReviewAction.Approved
                    ? $"Approved version {r.ReportVersion.VersionNumber}"
                    : $"Requested changes on version {r.ReportVersion.VersionNumber}: {r.Comment}"
            })
            .ToListAsync(ct);

        var submissionsCount = await _db.ReportVersions.CountAsync(ct);
        var reviewsCount = await _db.ReportReviews.CountAsync(ct);

        var merged = submissions
            .Concat(reviews)
            .OrderByDescending(a => a.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<ActivityFeedItemDto>
        {
            Items = merged,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = submissionsCount + reviewsCount
        };
    }

    private static (DateOnly From, DateOnly To) ResolveRange(DateRangeQuery query)
    {
        var to = query.WeekEndDate ?? CurrentWeekStart();
        var from = query.WeekStartDate ?? to.AddDays(-7 * 7); // default: trailing 8 weeks
        return (from, to);
    }
}
