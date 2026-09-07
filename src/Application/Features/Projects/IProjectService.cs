using Application.Common.Models;
using Application.Features.Projects.Dtos;

namespace Application.Features.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetAllAsync(ProjectQueryParameters query, CancellationToken ct = default);
    Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(Guid createdByUserId, CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);

    /// <summary>Soft delete: sets IsActive = false. A hard delete would violate the
    /// Restrict FK from Report -&gt; Project, and the domain already models an active flag.</summary>
    Task DeactivateAsync(Guid id, CancellationToken ct = default);

    Task<ProjectAssignmentDto> AssignUserAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}
