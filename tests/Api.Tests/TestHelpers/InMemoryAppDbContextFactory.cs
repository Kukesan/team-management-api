using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Builds an AppDbContext backed by EF Core's InMemory provider, isolated per call
/// (unique database name), so ReportService/UserService/ProjectService can be
/// exercised against IAppDbContext without a real Postgres instance. Identity
/// entities (ApplicationUser, IdentityRole) are added directly via the DbSet in
/// tests -- bypassing UserManager/RoleManager, which is fine for tests that only
/// need the FK/navigation shape, not password hashing or Identity's own validation.
/// </summary>
public static class InMemoryAppDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
