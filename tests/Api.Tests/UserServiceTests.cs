using Api.Tests.TestHelpers;
using Application.Common.Exceptions;
using Application.Features.Users;
using Application.Features.Users.Dtos;
using Domain.Constants;

namespace Api.Tests;

/// <summary>
/// RBAC-adjacent business rules on user management: an Admin must not be able to
/// strand the system without any Admin, and must not be able to deactivate themself
/// through the same endpoint used to remove other accounts.
/// </summary>
public class UserServiceTests
{
    [Fact]
    public async Task DeactivateAsync_LastRemainingAdmin_ThrowsConflict()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.TeamMember);
        var onlyAdmin = await host.CreateUserAsync("Sole Admin", Roles.Admin);
        var service = new UserService(host.UserManager);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeactivateAsync(actingAdminId: Guid.NewGuid(), userId: onlyAdmin.Id));
    }

    [Fact]
    public async Task DeactivateAsync_OneOfSeveralAdmins_Succeeds()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.TeamMember);
        var admin1 = await host.CreateUserAsync("Admin One", Roles.Admin);
        var admin2 = await host.CreateUserAsync("Admin Two", Roles.Admin);
        var service = new UserService(host.UserManager);

        await service.DeactivateAsync(actingAdminId: admin2.Id, userId: admin1.Id);

        var deactivated = await host.UserManager.FindByIdAsync(admin1.Id.ToString());
        Assert.False(deactivated!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_OwnAccount_ThrowsConflict()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.TeamMember);
        var admin1 = await host.CreateUserAsync("Admin One", Roles.Admin);
        await host.CreateUserAsync("Admin Two", Roles.Admin); // another admin exists -- rules out the last-admin path
        var service = new UserService(host.UserManager);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeactivateAsync(actingAdminId: admin1.Id, userId: admin1.Id));
    }

    [Fact]
    public async Task ChangeRoleAsync_DemotingLastRemainingAdmin_ThrowsConflict()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.Manager, Roles.TeamMember);
        var onlyAdmin = await host.CreateUserAsync("Sole Admin", Roles.Admin);
        var service = new UserService(host.UserManager);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ChangeRoleAsync(actingAdminId: Guid.NewGuid(), userId: onlyAdmin.Id, newRole: Roles.Manager));
    }

    [Fact]
    public async Task ChangeRoleAsync_PromotingATeamMemberToManager_Succeeds()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.Manager, Roles.TeamMember);
        var member = await host.CreateUserAsync("Regular Member", Roles.TeamMember);
        var admin = await host.CreateUserAsync("Admin", Roles.Admin);
        var service = new UserService(host.UserManager);

        var result = await service.ChangeRoleAsync(admin.Id, member.Id, Roles.Manager);

        Assert.Equal(new[] { Roles.Manager }, result.Roles);
    }
}
