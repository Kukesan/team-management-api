namespace Application.Features.Auth.Dtos;

/// <summary>
/// Self-registration always creates a TeamMember — there is no Role field here by
/// design. Promotion to Manager/Admin happens only via POST /api/users/{id}/role
/// (Admin-only) or seed data.
/// </summary>
public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
