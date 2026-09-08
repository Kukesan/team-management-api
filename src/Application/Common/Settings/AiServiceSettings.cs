namespace Application.Common.Settings;

/// <summary>Config for team-management-api's outbound calls to the FastAPI AI service
/// (team-management-ai). InternalApiKey is sent as X-Internal-Api-Key on every call so
/// the AI service can trust requests as coming from this gateway.</summary>
public class AiServiceSettings
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = string.Empty;
    public string InternalApiKey { get; set; } = string.Empty;
}
