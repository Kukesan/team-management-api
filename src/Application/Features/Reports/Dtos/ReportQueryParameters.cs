using Application.Common.Models;
using Domain.Enums;

namespace Application.Features.Reports.Dtos;

/// <summary>Filters for GET /api/reports/mine.</summary>
public class MyReportsQueryParameters : PaginationParameters
{
    public Guid? ProjectId { get; set; }
    public ReportStatus? Status { get; set; }
    public DateOnly? WeekStartDate { get; set; }
}

/// <summary>Filters for GET /api/reports (manager/admin, across all team members).</summary>
public class ManagerReportsQueryParameters : PaginationParameters
{
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public ReportStatus? Status { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
