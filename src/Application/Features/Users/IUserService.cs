using Application.Common.Models;
using Application.Features.Users.Dtos;

namespace Application.Features.Users;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetAllAsync(UserQueryParameters query, CancellationToken ct = default);
    Task<UserListItemDto> ChangeRoleAsync(Guid actingAdminId, Guid userId, string newRole, CancellationToken ct = default);

    /// <summary>Soft delete: sets IsActive = false, which also blocks login (see AuthService.LoginAsync).</summary>
    Task DeactivateAsync(Guid actingAdminId, Guid userId, CancellationToken ct = default);

    /// <summary>Creates the account immediately with a generated temporary password (no email
    /// infrastructure exists to send a real invite link -- see InvitedUserDto).</summary>
    Task<InvitedUserDto> InviteAsync(Guid actingAdminId, InviteUserRequest request, CancellationToken ct = default);

    /// <summary>Issues a fresh temporary password for a user who is locked out, replacing their
    /// current one. Same underlying mechanism as InviteAsync.</summary>
    Task<PasswordResetResultDto> ResetPasswordAsync(Guid actingAdminId, Guid userId, CancellationToken ct = default);
}
