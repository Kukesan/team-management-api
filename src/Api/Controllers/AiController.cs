using Api.Extensions;
using Application.Features.Ai;
using Application.Features.Ai.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Gateway to the FastAPI AI service (team-management-ai) -- the browser never talks
/// to it directly. Chat/summary are Manager/Admin only (team-wide oversight, mirroring
/// DashboardController); Help is open to every authenticated role since it's static
/// product how-to content with no DB access, so the role restriction lives per-action
/// here rather than on the controller.
/// </summary>
[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("chat")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<ChatResponseDto>> Chat(ChatRequestDto request, CancellationToken ct)
    {
        var result = await _aiService.ChatAsync(User.GetUserId(), User.GetFullName(), User.GetRoles(), request, ct);
        return Ok(result);
    }

    [HttpGet("summary")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<SummaryResponseDto>> GetSummary(
        [FromQuery] DateOnly week, [FromQuery] Guid? projectId, CancellationToken ct)
    {
        var result = await _aiService.GetSummaryAsync(User.GetUserId(), User.GetFullName(), User.GetRoles(), week, projectId, ct);
        return Ok(result);
    }

    [HttpPost("help")]
    public async Task<ActionResult<HelpResponseDto>> Help(HelpRequestDto request, CancellationToken ct)
    {
        var result = await _aiService.HelpAsync(User.GetUserId(), User.GetFullName(), User.GetRoles(), request, ct);
        return Ok(result);
    }
}
