using Application.Features.Ai.Dtos;

namespace Application.Features.Ai;

/// <summary>Proxies to the FastAPI AI service (team-management-ai). Callers pass the
/// already-authenticated caller's identity so it can be forwarded as trusted headers --
/// this service does not re-derive it.</summary>
public interface IAiService
{
    Task<ChatResponseDto> ChatAsync(
        Guid userId, string userName, IList<string> roles, ChatRequestDto request, CancellationToken ct = default);

    Task<SummaryResponseDto> GetSummaryAsync(
        Guid userId, string userName, IList<string> roles, DateOnly weekStart, Guid? projectId, CancellationToken ct = default);

    /// <summary>Product how-to Q&amp;A, open to every role (not just Manager/Admin) -- the FastAPI
    /// side answers purely from a role-filtered documentation file, with no DB access.</summary>
    Task<HelpResponseDto> HelpAsync(
        Guid userId, string userName, IList<string> roles, HelpRequestDto request, CancellationToken ct = default);
}
