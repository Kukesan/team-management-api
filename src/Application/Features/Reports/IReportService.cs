using Application.Common.Models;
using Application.Features.Reports.Dtos;

namespace Application.Features.Reports;

public interface IReportService
{
    Task<ReportDetailDto> CreateAsync(Guid userId, CreateReportRequest request, CancellationToken ct = default);

    Task<ReportDetailDto> UpdateAsync(Guid userId, Guid reportId, UpdateReportRequest request, CancellationToken ct = default);

    Task<ReportDetailDto> SubmitAsync(Guid userId, Guid reportId, CancellationToken ct = default);

    Task<PagedResult<ReportListItemDto>> GetMineAsync(Guid userId, MyReportsQueryParameters query, CancellationToken ct = default);

    /// <summary>Owner or Manager/Admin only — enforced here, not just by the controller/query.</summary>
    Task<ReportDetailDto> GetByIdAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, CancellationToken ct = default);

    Task<IReadOnlyList<ReportVersionSummaryDto>> GetVersionsAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, CancellationToken ct = default);

    Task<ReportVersionDetailDto> GetVersionAsync(Guid requestingUserId, IList<string> requestingUserRoles, Guid reportId, Guid versionId, CancellationToken ct = default);

    Task<PagedResult<ReportListItemDto>> GetAllForManagerAsync(ManagerReportsQueryParameters query, CancellationToken ct = default);

    /// <summary>
    /// The ONLY way a manager can mutate a report: this touches Status and creates a
    /// ReportReview row. It never writes to TaskItems/Blockers/Achievements/HoursBreakdown.
    /// </summary>
    Task<ReportDetailDto> ReviewAsync(Guid reviewerId, Guid reportId, ReviewRequest request, CancellationToken ct = default);
}
