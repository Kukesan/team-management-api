using System.Reflection;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

/// <summary>
/// Reflection-based guard against the exact failure the spec calls out: "a team member
/// must never be able to access another team member's report data or a manager-only
/// endpoint". This doesn't exercise the pipeline end-to-end (see ReportServiceTests for
/// the behavioral RBAC coverage) -- it asserts the [Authorize]/[Authorize(Roles=...)]
/// attributes controllers rely on are still declared as expected, so a future edit that
/// accidentally drops one is caught here instead of in production.
/// </summary>
public class ControllerAuthorizationTests
{
    private static (bool RequiresAuthentication, string? RequiredRolesCsv, bool AllowAnonymous) GetEffectiveAuthorization(
        Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{controllerType.Name}.{methodName} not found -- did the action get renamed?");

        var allowAnonymous =
            method.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
            controllerType.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        var effectiveAuthorize =
            method.GetCustomAttribute<AuthorizeAttribute>() ??
            controllerType.GetCustomAttribute<AuthorizeAttribute>();

        return (
            RequiresAuthentication: effectiveAuthorize is not null && !allowAnonymous,
            RequiredRolesCsv: effectiveAuthorize?.Roles,
            AllowAnonymous: allowAnonymous);
    }

    public static IEnumerable<object?[]> ManagerOrAdminOnlyEndpoints => new[]
    {
        new object?[] { typeof(ReportsController), nameof(ReportsController.GetAll) },
        new object?[] { typeof(ReportsController), nameof(ReportsController.Review) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.Create) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.Update) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.Deactivate) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.AssignUser) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.AssignUsers) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.UnassignUser) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.UnassignUsers) },
        new object?[] { typeof(UsersController), nameof(UsersController.GetAll) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetSummary) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetTasksTrend) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetStatusByMember) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetWorkloadByProject) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetTimeByTaskType) },
        new object?[] { typeof(DashboardController), nameof(DashboardController.GetActivityFeed) },
        new object?[] { typeof(AiController), nameof(AiController.Chat) },
        new object?[] { typeof(AiController), nameof(AiController.GetSummary) },
    };

    [Theory]
    [MemberData(nameof(ManagerOrAdminOnlyEndpoints))]
    public void ManagerOrAdminOnlyEndpoint_RejectsPlainTeamMember(Type controllerType, string methodName)
    {
        var (requiresAuth, rolesCsv, allowAnonymous) = GetEffectiveAuthorization(controllerType, methodName);

        Assert.True(requiresAuth, $"{controllerType.Name}.{methodName} must require authentication.");
        Assert.False(allowAnonymous, $"{controllerType.Name}.{methodName} must not allow anonymous access.");
        Assert.False(string.IsNullOrWhiteSpace(rolesCsv), $"{controllerType.Name}.{methodName} must restrict roles.");

        var roles = rolesCsv!.Split(',', StringSplitOptions.TrimEntries);
        Assert.DoesNotContain("TeamMember", roles);
        Assert.True(roles.Contains("Manager") || roles.Contains("Admin"),
            $"{controllerType.Name}.{methodName} should allow Manager and/or Admin.");
    }

    public static IEnumerable<object?[]> AdminOnlyEndpoints => new[]
    {
        new object?[] { typeof(UsersController), nameof(UsersController.ChangeRole) },
        new object?[] { typeof(UsersController), nameof(UsersController.Deactivate) },
    };

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public void AdminOnlyEndpoint_RejectsManagerAndTeamMember(Type controllerType, string methodName)
    {
        var (requiresAuth, rolesCsv, allowAnonymous) = GetEffectiveAuthorization(controllerType, methodName);

        Assert.True(requiresAuth);
        Assert.False(allowAnonymous);
        var roles = (rolesCsv ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "Admin" }, roles);
    }

    public static IEnumerable<object?[]> AnonymousEndpoints => new[]
    {
        new object?[] { typeof(AuthController), nameof(AuthController.Register) },
        new object?[] { typeof(AuthController), nameof(AuthController.Login) },
    };

    [Theory]
    [MemberData(nameof(AnonymousEndpoints))]
    public void AuthEntryPoint_AllowsAnonymous(Type controllerType, string methodName)
    {
        var (_, _, allowAnonymous) = GetEffectiveAuthorization(controllerType, methodName);

        Assert.True(allowAnonymous, $"{controllerType.Name}.{methodName} must stay reachable by an unauthenticated caller.");
    }

    public static IEnumerable<object?[]> AnyAuthenticatedRoleEndpoints => new[]
    {
        // Every role may read/submit their own reports and read the project list.
        new object?[] { typeof(ReportsController), nameof(ReportsController.Create) },
        new object?[] { typeof(ReportsController), nameof(ReportsController.GetMine) },
        new object?[] { typeof(ProjectsController), nameof(ProjectsController.GetAll) },
        // /ai/help is intentionally open to every role (static docs, no DB access).
        new object?[] { typeof(AiController), nameof(AiController.Help) },
    };

    [Theory]
    [MemberData(nameof(AnyAuthenticatedRoleEndpoints))]
    public void AnyAuthenticatedRoleEndpoint_RequiresLoginButNoSpecificRole(Type controllerType, string methodName)
    {
        var (requiresAuth, rolesCsv, allowAnonymous) = GetEffectiveAuthorization(controllerType, methodName);

        Assert.True(requiresAuth, $"{controllerType.Name}.{methodName} must still require a logged-in user.");
        Assert.False(allowAnonymous);
        Assert.True(string.IsNullOrWhiteSpace(rolesCsv), $"{controllerType.Name}.{methodName} should not be role-restricted.");
    }

    [Fact]
    public void AllControllers_ExceptAuth_RequireAuthenticationByDefault()
    {
        // No diagnostics/template-controller carve-out needed: WeatherForecastController
        // (anonymous, unauthenticated dead template code) has been removed.
        var controllerTypes = typeof(ReportsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t != typeof(AuthController));

        foreach (var controllerType in controllerTypes)
        {
            var hasClassAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>() is not null;
            Assert.True(hasClassAuthorize, $"{controllerType.Name} has no class-level [Authorize] -- every action must opt in individually or this is a public-by-default risk.");
        }
    }
}
