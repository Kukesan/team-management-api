namespace Application.Common.Models;

/// <summary>Consistent error shape returned by both the validation filter and the global exception middleware.</summary>
public class ApiErrorResponse
{
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}
