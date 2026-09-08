using Api.Extensions;
using Application.Common.Models;
using Application.Features.Projects;
using Application.Features.Projects.Dtos;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetAll([FromQuery] ProjectQueryParameters query, CancellationToken ct)
    {
        var result = await _projectService.GetAllAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var result = await _projectService.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var result = await _projectService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>Soft delete (sets IsActive = false) — a hard delete would violate the
    /// Restrict FK from Report, and the domain already models an active flag.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _projectService.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<ProjectAssignmentDto>> AssignUser(Guid id, AssignUserRequest request, CancellationToken ct)
    {
        var result = await _projectService.AssignUserAsync(id, request.UserId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-bulk")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<ActionResult<IList<ProjectAssignmentDto>>> AssignUsers(Guid id, AssignUsersRequest request, CancellationToken ct)
    {
        var result = await _projectService.AssignUsersAsync(id, request.UserIds, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/assign/{userId:guid}")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<IActionResult> UnassignUser(Guid id, Guid userId, CancellationToken ct)
    {
        await _projectService.UnassignUserAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unassign-bulk")]
    [Authorize(Roles = Roles.ManagerOrAdminCsv)]
    public async Task<IActionResult> UnassignUsers(Guid id, UnassignUsersRequest request, CancellationToken ct)
    {
        await _projectService.UnassignUsersAsync(id, request.UserIds, ct);
        return NoContent();
    }
}
