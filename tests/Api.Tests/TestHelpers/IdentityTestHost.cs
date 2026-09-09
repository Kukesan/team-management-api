using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Minimal DI host that wires up ASP.NET Core Identity (UserManager/RoleManager) over
/// an isolated InMemory AppDbContext, so UserService -- which depends on UserManager,
/// not IAppDbContext directly -- can be tested without a real Postgres/Identity host.
/// </summary>
public sealed class IdentityTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public UserManager<ApplicationUser> UserManager { get; }
    public RoleManager<IdentityRole<Guid>> RoleManager { get; }

    private IdentityTestHost(ServiceProvider provider)
    {
        _provider = provider;
        UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    }

    public static async Task<IdentityTestHost> CreateAsync(params string[] roles)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var host = new IdentityTestHost(provider);

        foreach (var role in roles)
        {
            if (!await host.RoleManager.RoleExistsAsync(role))
            {
                await host.RoleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        return host;
    }

    public async Task<ApplicationUser> CreateUserAsync(string fullName, string role, string password = "Password123")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.local",
            UserName = $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.local",
            IsActive = true
        };

        var result = await UserManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        await UserManager.AddToRoleAsync(user, role);
        return user;
    }

    public void Dispose() => _provider.Dispose();
}
