using Application.Common.Models;

namespace Application.Features.Projects.Dtos;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IList<ProjectMemberDto> AssignedUsers { get; set; } = new List<ProjectMemberDto>();
}

public class ProjectMemberDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class AssignUserRequest
{
    public Guid UserId { get; set; }
}

public class AssignUsersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

public class UnassignUsersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

public class ProjectAssignmentDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public class ProjectQueryParameters : PaginationParameters
{
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
