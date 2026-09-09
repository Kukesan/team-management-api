using System.Security.Cryptography;
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

    public async Task<InvitedUserDto> InviteAsync(Guid actingAdminId, InviteUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictException($"A user with email '{request.Email}' already exists.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            // No email-verification flow exists yet -- an admin-invited account is trusted
            // by construction (only an Admin can call this endpoint).
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["Email"] = createResult.Errors.Select(e => e.Description).ToArray()
            });
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        return new InvitedUserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = new List<string> { request.Role },
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            TemporaryPassword = temporaryPassword
        };
    }

    public async Task<PasswordResetResultDto> ResetPasswordAsync(Guid actingAdminId, Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        var temporaryPassword = GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!result.Succeeded)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["Password"] = result.Errors.Select(e => e.Description).ToArray()
            });
        }

        return new PasswordResetResultDto { UserId = user.Id, TemporaryPassword = temporaryPassword };
    }

    private async Task<bool> IsLastAdminAsync(Guid excludingUserId, CancellationToken ct)
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(a => a.Id != excludingUserId) == 0;
    }

    /// <summary>
    /// Generates a random password guaranteed to satisfy Identity's policy (>=8 chars,
    /// upper+lower+digit -- see Program.cs/RegisterRequestValidator). Never logged; returned
    /// to the caller exactly once so it can be handed to the new/locked-out user out of band.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no ambiguous I/O
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        const int length = 12;

        var randomBytes = RandomNumberGenerator.GetBytes(length * 2);
        var chars = new char[length];
        chars[0] = upper[randomBytes[0] % upper.Length];
        chars[1] = lower[randomBytes[1] % lower.Length];
        chars[2] = digits[randomBytes[2] % digits.Length];
        for (var i = 3; i < length; i++)
        {
            chars[i] = all[randomBytes[i] % all.Length];
        }

        // Fisher-Yates shuffle (using the second half of the random bytes) so the fixed
        // upper/lower/digit slots above aren't always in positions 0-2.
        for (var i = length - 1; i > 0; i--)
        {
            var j = randomBytes[length + i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
