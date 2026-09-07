using Application.Common.Models;

namespace Application.Features.Users.Dtos;

public class UserListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChangeRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class UserQueryParameters : PaginationParameters
{
    public string? Search { get; set; }
    public string? Role { get; set; }
}
