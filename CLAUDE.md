# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Build, test, and run from the repo root (`TeamManagement.sln`):

```
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"   # run a single test
dotnet run --project src/Api
```

EF Core migrations (run from repo root, targeting the `Infrastructure` project with `Api` as the startup project for configuration):

```
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Required secrets are not in `appsettings.json` (values are blank there). Set them via `dotnet user-secrets` in `src/Api` for local dev, or via environment variables in other environments:
- `ConnectionStrings:DefaultConnection` (Postgres)
- `Jwt:Key`
- `AiService:BaseUrl`, `AiService:InternalApiKey` (only needed for the AI chat/summary endpoints)

The app throws on startup if `ConnectionStrings:DefaultConnection` or `Jwt:Key` is missing.

Note: `tests/Api.Tests` currently only contains the default xUnit template test (`UnitTest1.cs`) — there is no real test suite yet.

## Architecture

Clean Architecture / layered solution, four projects under `src/` plus `tests/Api.Tests`, with strict dependency direction: `Api` → `Infrastructure` → `Application` → `Domain` (`Infrastructure` and `Application` both reference `Domain`; `Api` references all three).

- **Domain** — entities (`Entities/`), enums, and the `Roles` constants (`TeamMember`, `Manager`, `Admin`, plus `Roles.ManagerOrAdminCsv` for `[Authorize(Roles = ...)]`). No dependencies on other layers.
- **Application** — business logic, organized by **feature folder** under `Features/<Name>/` (`Auth`, `Users`, `Projects`, `Reports`, `Dashboard`, `Ai`), each with a service class + `I<Name>Service` interface, `Dtos/`, and FluentValidation `Validators/`. This is a service-layer architecture, not MediatR/CQRS — controllers call service interfaces directly. `Common/` holds cross-cutting pieces: `IAppDbContext` (an interface over `AppDbContext` so Application stays decoupled from EF/Infrastructure), typed exceptions (`NotFoundException`, `ConflictException`, `ForbiddenException`, `UnauthorizedAppException`, `ValidationAppException`), `ApiErrorResponse`/`PagedResult`/`PaginationParameters`, and strongly-typed settings (`JwtSettings`, `AiServiceSettings`).
- **Infrastructure** — `Persistence/AppDbContext.cs` (EF Core + Npgsql), per-entity `IEntityTypeConfiguration<T>` classes under `Persistence/Configurations/`, `Persistence/Migrations/`, `Persistence/DbInitializer.cs` (role seeding always; dev admin user + demo users/projects/reports seeded only in Development), and `Auth/JwtTokenService.cs`.
- **Api** — thin controllers under `Controllers/` (one per feature, mirroring `Application/Features`), each `[Authorize]` by default with per-action `[Authorize(Roles = Roles.ManagerOrAdminCsv)]` overrides where needed. All composition happens in `Program.cs` (DI registrations, JWT bearer auth, CORS, Swagger, DbContext, Identity, seeding on startup).

### Cross-cutting request pipeline

- `ValidationFilter` (global `IAsyncActionFilter`) auto-runs any registered `IValidator<T>` against action arguments before the action executes — controllers don't call validators manually. Failures throw `ValidationAppException`.
- `ExceptionHandlingMiddleware` maps the typed exceptions above to HTTP status codes and a consistent `{title, status, errors}` JSON body (`ApiErrorResponse`), reusing the same JSON options (camelCase) as normal MVC responses. Non-typed exceptions become a logged 500.
- Malformed/unbindable request bodies are routed through the same `ApiErrorResponse` shape via `ApiBehaviorOptions.InvalidModelStateResponseFactory` in `Program.cs`, rather than the default ASP.NET `ProblemDetails`.
- Enums are serialized/deserialized as strings (`JsonStringEnumConverter`), not ints.
- `ClaimsPrincipalExtensions` (`Api/Extensions`) provides `GetUserId()`, `GetRoles()`, `GetFullName()` on `ClaimsPrincipal` — the standard way controllers pull the current user's identity out of `User`.

### Auth model

JWT bearer auth (`Microsoft.AspNetCore.Identity` core, no UI) with `ApplicationUser : IdentityUser<Guid>` and `IdentityRole<Guid>`. Password rules are enforced in two places kept in sync: ASP.NET Identity options in `Program.cs` and `RegisterRequestValidator` (the latter is the source of truth for what's surfaced to API clients). Roles are `TeamMember`, `Manager`, `Admin`; most manager-only endpoints use `[Authorize(Roles = Roles.ManagerOrAdminCsv)]` rather than a named policy.

### AI features

`Application/Features/Ai/AiService.cs` calls out to a **separate external FastAPI service** (not part of this repo) for chat and weekly-summary generation, configured via `AiServiceSettings` (`BaseUrl`, `InternalApiKey`) and a named `HttpClient` (`AiService.HttpClientName`) with a 60s timeout (LLM round trips run longer than typical CRUD calls). Requests carry the caller's identity via `X-Internal-Api-Key`/`X-User-Id`/`X-User-Name`/`X-User-Roles` headers instead of forwarding the JWT. The external service's JSON is snake_case (pydantic); `AiService` maps it to internal PascalCase DTOs via private response classes with explicit `JsonPropertyName` attributes.

### Reports domain (most complex feature)

`Report` has versioned content (`ReportVersion`) plus child collections per version (`ReportTaskItem`, `ReportBlocker`, `ReportAchievement`, `ReportHoursBreakdown`) and a review trail (`ReportReview`, driven by `ReviewAction`/`ReportStatus` enums). `ReportsController`/`ReportService` distinguish self-service endpoints (create/update/submit/"mine", scoped to `User.GetUserId()`) from manager endpoints (`GetAll`, `Review`, both `[Authorize(Roles = Roles.ManagerOrAdminCsv)]`), and getters that branch on `User.GetRoles()` to decide whether the caller may view another user's report/version.
