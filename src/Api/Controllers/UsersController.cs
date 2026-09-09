using Api.Extensions;
using Application.Common.Models;
using Application.Features.Users;
using Application.Features.Users.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Managers can read the roster too — they need it to assign team members to projects.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetAll([FromQuery] UserQueryParameters query, CancellationToken ct)
    {
        var result = await _userService.GetAllAsync(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/role")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserListItemDto>> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct)
    {
        var result = await _userService.ChangeRoleAsync(User.GetUserId(), id, request.Role, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _userService.DeactivateAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Creates the account immediately with a generated temporary password -- see
    /// InvitedUserDto for why this isn't a real email-invite flow.</summary>
    [HttpPost("invite")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<InvitedUserDto>> Invite(InviteUserRequest request, CancellationToken ct)
    {
        var result = await _userService.InviteAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PasswordResetResultDto>> ResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _userService.ResetPasswordAsync(User.GetUserId(), id, ct);
        return Ok(result);
    }
}
