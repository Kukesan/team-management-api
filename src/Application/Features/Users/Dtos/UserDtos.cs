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

public class InviteUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// TRADEOFF: there is no outbound-email infrastructure in this stack, so "inviting" a
/// user creates the account immediately with a server-generated temporary password
/// rather than sending an email invite link. The temporary password is returned here
/// exactly once -- it is never stored in retrievable form and never logged -- so the
/// admin can hand it to the new user out of band. Same shape is reused by
/// ResetPasswordAsync since it's the same underlying capability.
/// </summary>
public class InvitedUserDto : UserListItemDto
{
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class PasswordResetResultDto
{
    public Guid UserId { get; set; }
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class UserQueryParameters : PaginationParameters
{
    public string? Search { get; set; }
    public string? Role { get; set; }
}
