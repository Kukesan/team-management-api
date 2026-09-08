using Api.Extensions;
using Application.Features.Ai;
using Application.Features.Ai.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Gateway to the FastAPI AI service (team-management-ai) -- the browser never talks
/// to it directly. Manager/Admin only, mirroring DashboardController: both are
/// team-wide oversight features, not something a team member's own report page needs.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.ManagerOrAdminCsv)]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponseDto>> Chat(ChatRequestDto request, CancellationToken ct)
    {
        var result = await _aiService.ChatAsync(User.GetUserId(), User.GetFullName(), User.GetRoles(), request, ct);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryResponseDto>> GetSummary(
        [FromQuery] DateOnly week, [FromQuery] Guid? projectId, CancellationToken ct)
    {
        var result = await _aiService.GetSummaryAsync(User.GetUserId(), User.GetFullName(), User.GetRoles(), week, projectId, ct);
        return Ok(result);
    }
}
