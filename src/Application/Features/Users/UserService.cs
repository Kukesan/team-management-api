using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Users.Dtos;
using Domain.Constants;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Application.Features.Users;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<PagedResult<UserListItemDto>> GetAllAsync(UserQueryParameters query, CancellationToken ct = default)
    {
        var users = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            users = users.Where(u => u.FullName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term));
        }

        users = query.SortBy?.ToLowerInvariant() switch
        {
            "email" => query.SortDescending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
            "createdat" => query.SortDescending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            _ => query.SortDescending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName)
        };

        var all = users.ToList();
        var withRoles = new List<UserListItemDto>();
        foreach (var user in all)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(query.Role) && !roles.Contains(query.Role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            withRoles.Add(new UserListItemDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Roles = roles,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        var totalCount = withRoles.Count;
        var page = withRoles
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<UserListItemDto>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UserListItemDto> ChangeRoleAsync(Guid actingAdminId, Guid userId, string newRole, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Contains(Roles.Admin) && newRole != Roles.Admin && await IsLastAdminAsync(userId, ct))
        {
            throw new ConflictException("Cannot change the role of the last remaining Admin.");
        }

        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await _userManager.AddToRoleAsync(user, newRole);

        return new UserListItemDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = new List<string> { newRole },
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task DeactivateAsync(Guid actingAdminId, Guid userId, CancellationToken ct = default)
    {
        if (actingAdminId == userId)
        {
            throw new ConflictException("You cannot deactivate your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin) && await IsLastAdminAsync(userId, ct))
        {
            throw new ConflictException("Cannot deactivate the last remaining Admin.");
        }

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
    }

    private async Task<bool> IsLastAdminAsync(Guid excludingUserId, CancellationToken ct)
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(a => a.Id != excludingUserId) == 0;
    }
}
