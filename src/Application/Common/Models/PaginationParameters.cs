namespace Application.Common.Models;

public class PaginationParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _page = 1;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Property name to sort by; interpretation is up to each query handler.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
