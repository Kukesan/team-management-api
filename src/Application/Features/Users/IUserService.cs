using Application.Common.Models;
using Application.Features.Users.Dtos;

namespace Application.Features.Users;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetAllAsync(UserQueryParameters query, CancellationToken ct = default);
    Task<UserListItemDto> ChangeRoleAsync(Guid actingAdminId, Guid userId, string newRole, CancellationToken ct = default);

    /// <summary>Soft delete: sets IsActive = false, which also blocks login (see AuthService.LoginAsync).</summary>
    Task DeactivateAsync(Guid actingAdminId, Guid userId, CancellationToken ct = default);
}
