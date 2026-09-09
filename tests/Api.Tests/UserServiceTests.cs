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

    [Fact]
    public async Task InviteAsync_NewEmail_CreatesAccountWithUsableTemporaryPassword()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.Manager, Roles.TeamMember);
        var admin = await host.CreateUserAsync("Admin", Roles.Admin);
        var service = new UserService(host.UserManager);

        var result = await service.InviteAsync(admin.Id, new InviteUserRequest
        {
            FullName = "New Hire",
            Email = "new.hire@test.local",
            Role = Roles.TeamMember
        });

        Assert.Equal("new.hire@test.local", result.Email);
        Assert.Equal(new[] { Roles.TeamMember }, result.Roles);
        Assert.NotEmpty(result.TemporaryPassword);

        // The returned password must actually satisfy Identity's own policy and let the
        // new account sign in -- not just look plausible.
        var created = await host.UserManager.FindByEmailAsync("new.hire@test.local");
        Assert.NotNull(created);
        var canSignIn = await host.UserManager.CheckPasswordAsync(created!, result.TemporaryPassword);
        Assert.True(canSignIn);
    }

    [Fact]
    public async Task InviteAsync_DuplicateEmail_ThrowsConflict()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.TeamMember);
        var admin = await host.CreateUserAsync("Admin", Roles.Admin);
        var existing = await host.CreateUserAsync("Existing Member", Roles.TeamMember);
        var service = new UserService(host.UserManager);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.InviteAsync(admin.Id, new InviteUserRequest
            {
                FullName = "Duplicate",
                Email = existing.Email!,
                Role = Roles.TeamMember
            }));
    }

    [Fact]
    public async Task ResetPasswordAsync_IssuesANewPasswordThatReplacesTheOldOne()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin, Roles.TeamMember);
        var admin = await host.CreateUserAsync("Admin", Roles.Admin);
        var member = await host.CreateUserAsync("Locked Out Member", Roles.TeamMember, password: "OldPassword1");
        var service = new UserService(host.UserManager);

        var result = await service.ResetPasswordAsync(admin.Id, member.Id);

        Assert.Equal(member.Id, result.UserId);
        var reloaded = await host.UserManager.FindByIdAsync(member.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, result.TemporaryPassword));
        Assert.False(await host.UserManager.CheckPasswordAsync(reloaded!, "OldPassword1"));
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownUser_ThrowsNotFound()
    {
        using var host = await IdentityTestHost.CreateAsync(Roles.Admin);
        var admin = await host.CreateUserAsync("Admin", Roles.Admin);
        var service = new UserService(host.UserManager);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ResetPasswordAsync(admin.Id, Guid.NewGuid()));
    }
}
