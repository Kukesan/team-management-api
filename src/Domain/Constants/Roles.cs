namespace Domain.Constants;

public static class Roles
{
    public const string TeamMember = "TeamMember";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly string[] All = { TeamMember, Manager, Admin };

    // Convenience group used by policies that treat Manager and Admin equivalently
    // for report-review and project-management authority.
    public static readonly string[] ManagerOrAdmin = { Manager, Admin };

    /// <summary>Comma-separated form for [Authorize(Roles = Roles.ManagerOrAdminCsv)].</summary>
    public const string ManagerOrAdminCsv = "Manager,Admin";
}
