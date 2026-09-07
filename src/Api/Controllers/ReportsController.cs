using Api.Extensions;
using Application.Common.Models;
using Application.Features.Reports;
using Application.Features.Reports.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpPost]
    public async Task<ActionResult<ReportDetailDto>> Create(CreateReportRequest request, CancellationToken ct)
    {
        var result = await _reportService.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReportDetailDto>> Update(Guid id, UpdateReportRequest request, CancellationToken ct)
    {
        var result = await _reportService.UpdateAsync(User.GetUserId(), id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ReportDetailDto>> Submit(Guid id, CancellationToken ct)
    {
        var result = await _reportService.SubmitAsync(User.GetUserId(), id, ct);
        return Ok(result);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ReportListItemDto>>> GetMine([FromQuery] MyReportsQueryParameters query, CancellationToken ct)
    {
        var result = await _reportService.GetMineAsync(User.GetUserId(), query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _reportService.GetByIdAsync(User.GetUserId(), User.GetRoles(), id, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<ReportVersionSummaryDto>>> GetVersions(Guid id, CancellationToken ct)
    {
        var result = await _reportService.GetVersionsAsync(User.GetUserId(), User.GetRoles(), id, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    public async Task<ActionResult<ReportVersionDetailDto>> GetVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        var result = await _reportService.GetVersionAsync(User.GetUserId(), User.GetRoles(), id, versionId, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<PagedResult<ReportListItemDto>>> GetAll([FromQuery] ManagerReportsQueryParameters query, CancellationToken ct)
    {
        var result = await _reportService.GetAllForManagerAsync(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<ReportDetailDto>> Review(Guid id, ReviewRequest request, CancellationToken ct)
    {
        var result = await _reportService.ReviewAsync(User.GetUserId(), id, request, ct);
        return Ok(result);
    }
}
