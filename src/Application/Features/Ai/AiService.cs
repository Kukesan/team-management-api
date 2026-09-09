using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Common.Exceptions;
using Application.Common.Settings;
using Application.Features.Ai.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Features.Ai;

public class AiService : IAiService
{
    public const string HttpClientName = "AiService";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceSettings _settings;
    private readonly ILogger<AiService> _logger;

    public AiService(IHttpClientFactory httpClientFactory, IOptions<AiServiceSettings> settings, ILogger<AiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
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

        var result = await PostAsync<FastApiChatResponse>(client, "/chat", body, ct);
        return new ChatResponseDto { Answer = result.Answer, ToolsUsed = result.ToolsUsed };
    }

    public async Task<SummaryResponseDto> GetSummaryAsync(
        Guid userId, string userName, IList<string> roles, DateOnly weekStart, Guid? projectId, CancellationToken ct = default)
    {
        var client = CreateClient(userId, userName, roles);

        var body = new { week_start = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), project_id = projectId };

        var result = await PostAsync<FastApiSummaryResponse>(client, "/summary", body, ct);

        return new SummaryResponseDto
        {
            SummaryMarkdown = result.SummaryMarkdown,
            WeekStart = DateOnly.Parse(result.WeekStart, CultureInfo.InvariantCulture),
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

        var result = await PostAsync<FastApiHelpResponse>(client, "/help", body, ct);
        return new HelpResponseDto { Answer = result.Answer };
    }

    /// <summary>
    /// Sends one POST to the AI service and maps every failure mode to a clean
    /// ExternalServiceException (-> HTTP 503) instead of letting the caller see a bare,
    /// unexplained 500: a timeout or dropped connection is retried once (transient network
    /// blips are the realistic failure mode for a co-located service restarting), a non-success
    /// HTTP response is logged with its real status/body and translated to a generic message,
    /// and an empty/malformed body is treated the same way. Nothing about the AI service's
    /// internals is ever surfaced to the client.
    /// </summary>
    private async Task<TResponse> PostAsync<TResponse>(HttpClient client, string path, object body, CancellationToken ct)
    {
        const int maxAttempts = 2;
        HttpResponseMessage? response = null;
        Exception? transientError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                response = await client.PostAsJsonAsync(path, body, ct);
                transientError = null;
                break;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // The HttpClient's own Timeout elapsed (not the caller cancelling the request).
                transientError = ex;
            }
            catch (HttpRequestException ex)
            {
                transientError = ex;
            }

            if (attempt < maxAttempts)
            {
                _logger.LogWarning(transientError, "AI service call to {Path} failed on attempt {Attempt}/{MaxAttempts}, retrying.", path, attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
        }

        if (response is null)
        {
            _logger.LogError(transientError, "AI service call to {Path} failed after {MaxAttempts} attempts.", path, maxAttempts);
            throw new ExternalServiceException("The AI assistant is temporarily unavailable. Please try again shortly.", transientError!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "AI service returned {StatusCode} for {Path}: {Body}", (int)response.StatusCode, path, responseBody);

                var message = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    // A gateway auth failure (bad/missing X-Internal-Api-Key, or the AI
                    // service's own role re-check tripping) is a configuration problem, not
                    // something the caller did wrong.
                    ? "The AI assistant is temporarily unavailable (service authentication issue)."
                    : "The AI assistant could not process this request. Please try again.";

                throw new ExternalServiceException(message);
            }

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
            if (result is null)
            {
                _logger.LogError("AI service returned an empty/unparseable body for {Path}.", path);
                throw new ExternalServiceException("The AI assistant returned an empty response. Please try again.");
            }

            return result;
        }
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
