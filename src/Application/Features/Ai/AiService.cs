using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Common.Settings;
using Application.Features.Ai.Dtos;
using Microsoft.Extensions.Options;

namespace Application.Features.Ai;

public class AiService : IAiService
{
    public const string HttpClientName = "AiService";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceSettings _settings;

    public AiService(IHttpClientFactory httpClientFactory, IOptions<AiServiceSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<ChatResponseDto> ChatAsync(
        Guid userId, string userName, IList<string> roles, ChatRequestDto request, CancellationToken ct = default)
    {
        var client = CreateClient(userId, userName, roles);

        var body = new
        {
            message = request.Message,
            history = request.History.Select(h => new { role = h.Role, content = h.Content })
        };

        using var response = await client.PostAsJsonAsync("/chat", body, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FastApiChatResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AI service returned an empty chat response.");

        return new ChatResponseDto { Answer = result.Answer, ToolsUsed = result.ToolsUsed };
    }

    public async Task<SummaryResponseDto> GetSummaryAsync(
        Guid userId, string userName, IList<string> roles, DateOnly weekStart, Guid? projectId, CancellationToken ct = default)
    {
        var client = CreateClient(userId, userName, roles);

        var body = new { week_start = weekStart.ToString("yyyy-MM-dd"), project_id = projectId };

        using var response = await client.PostAsJsonAsync("/summary", body, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FastApiSummaryResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AI service returned an empty summary response.");

        return new SummaryResponseDto
        {
            SummaryMarkdown = result.SummaryMarkdown,
            WeekStart = DateOnly.Parse(result.WeekStart),
            ProjectId = result.ProjectId,
            GeneratedAt = result.GeneratedAt
        };
    }

    public async Task<HelpResponseDto> HelpAsync(
        Guid userId, string userName, IList<string> roles, HelpRequestDto request, CancellationToken ct = default)
    {
        var client = CreateClient(userId, userName, roles);

        var body = new
        {
            message = request.Message,
            history = request.History.Select(h => new { role = h.Role, content = h.Content })
        };

        using var response = await client.PostAsJsonAsync("/help", body, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FastApiHelpResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AI service returned an empty help response.");

        return new HelpResponseDto { Answer = result.Answer };
    }

    private HttpClient CreateClient(Guid userId, string userName, IList<string> roles)
    {
        // BaseAddress/Timeout are configured once via AddHttpClient(HttpClientName, ...) in
        // Program.cs; only the per-call identity headers are set here.
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", _settings.InternalApiKey);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Name", userName);
        client.DefaultRequestHeaders.Add("X-User-Roles", string.Join(",", roles));
        return client;
    }

    // Shapes match FastAPI's snake_case JSON responses (pydantic default) -- explicit
    // JsonPropertyName since System.Text.Json's default matching is case-sensitive and
    // these C# property names are PascalCase. Kept private to this class rather than
    // exposed as public DTOs; callers only ever see the mapped Chat/SummaryResponseDto above.
    private class FastApiChatResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("tools_used")]
        public IList<string> ToolsUsed { get; set; } = new List<string>();
    }

    private class FastApiSummaryResponse
    {
        [JsonPropertyName("summary_markdown")]
        public string SummaryMarkdown { get; set; } = string.Empty;

        [JsonPropertyName("week_start")]
        public string WeekStart { get; set; } = string.Empty;

        [JsonPropertyName("project_id")]
        public Guid? ProjectId { get; set; }

        [JsonPropertyName("generated_at")]
        public DateTime GeneratedAt { get; set; }
    }

    private class FastApiHelpResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;
    }
}
