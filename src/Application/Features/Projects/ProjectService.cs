using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Projects.Dtos;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Projects;

public class ProjectService : IProjectService
{
    private readonly IAppDbContext _db;

    public ProjectService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProjectDto>> GetAllAsync(ProjectQueryParameters query, CancellationToken ct = default)
    {
        var projects = _db.Projects.AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Assignments).ThenInclude(a => a.User)
            .AsQueryable();

        if (query.IsActive.HasValue)
        {
            projects = projects.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            projects = projects.Where(p => p.Name.ToLower().Contains(term));
        }

        projects = query.SortBy?.ToLowerInvariant() switch
        {
            "createdat" => query.SortDescending ? projects.OrderByDescending(p => p.CreatedAt) : projects.OrderBy(p => p.CreatedAt),
            "isactive" => query.SortDescending ? projects.OrderByDescending(p => p.IsActive) : projects.OrderBy(p => p.IsActive),
            _ => query.SortDescending ? projects.OrderByDescending(p => p.Name) : projects.OrderBy(p => p.Name)
        };

        var totalCount = await projects.CountAsync(ct);

        var items = await projects
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => MapDto(p))
            .ToListAsync(ct);

        return new PagedResult<ProjectDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Assignments).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        return MapDto(project);
    }

    public async Task<ProjectDto> CreateAsync(Guid createdByUserId, CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedById = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(project.Id, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        project.Name = request.Name;
        project.Description = request.Description;
        project.IsActive = request.IsActive;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(project.Id, ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        project.IsActive = false;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ProjectAssignmentDto> AssignUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw new NotFoundException("Project", projectId);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            throw new NotFoundException("User", userId);
        }

        var alreadyAssigned = await _db.ProjectAssignments
            .AnyAsync(a => a.ProjectId == projectId && a.UserId == userId, ct);
        if (alreadyAssigned)
        {
            throw new ConflictException("User is already assigned to this project.");
        }

        var assignment = new ProjectAssignment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow
        };

        _db.ProjectAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        return new ProjectAssignmentDto
        {
            Id = assignment.Id,
            ProjectId = projectId,
            UserId = userId,
            UserFullName = user.FullName,
            AssignedAt = assignment.AssignedAt
        };
    }

    public async Task<IList<ProjectAssignmentDto>> AssignUsersAsync(Guid projectId, IList<Guid> userIds, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw new NotFoundException("Project", projectId);
        }

        var distinctUserIds = userIds.Distinct().ToList();

        var users = await _db.Users
            .Where(u => distinctUserIds.Contains(u.Id))
            .ToListAsync(ct);

        var missingIds = distinctUserIds.Except(users.Select(u => u.Id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new NotFoundException($"User(s) with id(s) '{string.Join(", ", missingIds)}' were not found.");
        }

        var alreadyAssignedIds = await _db.ProjectAssignments
            .Where(a => a.ProjectId == projectId && distinctUserIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToListAsync(ct);

        // Already-assigned users are skipped rather than rejected, so re-selecting an
        // existing member alongside new ones doesn't fail the whole batch.
        var toAssign = users.Where(u => !alreadyAssignedIds.Contains(u.Id)).ToList();
        var now = DateTime.UtcNow;

        var assignments = toAssign.Select(u => new ProjectAssignment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = u.Id,
            AssignedAt = now
        }).ToList();

        if (assignments.Count > 0)
        {
            _db.ProjectAssignments.AddRange(assignments);
            await _db.SaveChangesAsync(ct);
        }

        return assignments.Select(a => new ProjectAssignmentDto
        {
            Id = a.Id,
            ProjectId = projectId,
            UserId = a.UserId,
            UserFullName = toAssign.First(u => u.Id == a.UserId).FullName,
            AssignedAt = a.AssignedAt
        }).ToList();
    }

    public async Task UnassignUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var assignment = await _db.ProjectAssignments
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.UserId == userId, ct)
            ?? throw new NotFoundException("This user is not assigned to the project.");

        _db.ProjectAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnassignUsersAsync(Guid projectId, IList<Guid> userIds, CancellationToken ct = default)
    {
        var distinctUserIds = userIds.Distinct().ToList();

        var assignments = await _db.ProjectAssignments
            .Where(a => a.ProjectId == projectId && distinctUserIds.Contains(a.UserId))
            .ToListAsync(ct);

        if (assignments.Count > 0)
        {
            _db.ProjectAssignments.RemoveRange(assignments);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static ProjectDto MapDto(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        IsActive = p.IsActive,
        CreatedById = p.CreatedById,
        CreatedByName = p.CreatedBy.FullName,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        AssignedUsers = p.Assignments
            .OrderBy(a => a.User.FullName)
            .Select(a => new ProjectMemberDto
            {
                UserId = a.UserId,
                FullName = a.User.FullName,
                Email = a.User.Email ?? string.Empty,
                AssignedAt = a.AssignedAt
            })
            .ToList()
    };
}
