namespace Application.Features.Ai.Dtos;

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public IList<ChatMessageDto> History { get; set; } = new List<ChatMessageDto>();
}

public class ChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public IList<string> ToolsUsed { get; set; } = new List<string>();
}

public class SummaryResponseDto
{
    public string SummaryMarkdown { get; set; } = string.Empty;
    public DateOnly WeekStart { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime GeneratedAt { get; set; }
}
