# team-management-api

ASP.NET Core 8 backend for the Weekly Report Generator & Team Dashboard app. Clean
Architecture / layered solution (`Domain` → `Application` → `Infrastructure` → `Api`),
JWT-based auth over ASP.NET Core Identity, PostgreSQL via EF Core. See
[CLAUDE.md](CLAUDE.md) for an architecture deep-dive.

The frontend lives in a separate repo, `team-management-front`. The AI chat/summary/help
features are served by a separate FastAPI service, `team-management-ai`, which this API
calls out to — see [Wiring up the AI service](#5-optional-wiring-up-the-ai-service) below.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A PostgreSQL database (a free [Supabase](https://supabase.com) Postgres project works
  well and is what this project was developed against; any Postgres 13+ instance is fine)
- The [`dotnet-ef` CLI tool](https://learn.microsoft.com/ef/core/cli/dotnet), if not
  already installed: `dotnet tool install --global dotnet-ef`

## 1. Install dependencies

```bash
dotnet restore
```

## 2. Configure secrets

Required values are intentionally left blank in `appsettings.json` — the app throws on
startup if `ConnectionStrings:DefaultConnection` or `Jwt:Key` is missing. Set them via
[.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for
local development (run these from the repo root, or `cd src/Api` first and drop
`--project src/Api`):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=...;Username=...;Password=..." --project src/Api
dotnet user-secrets set "Jwt:Key" "<any long random string, 32+ chars>" --project src/Api
```

In any non-Development environment, set the equivalent environment variables instead
(`ConnectionStrings__DefaultConnection`, `Jwt__Key` — double underscore is the standard
ASP.NET Core config-section separator).

Optional secrets:

| Secret | Purpose | Default if unset |
| --- | --- | --- |
| `Seed:AdminEmail` / `Seed:AdminPassword` | Dev-only bootstrap admin account (see step 4) | `admin@teammanagement.local` / `ChangeMe123!` |
| `AiService:BaseUrl` / `AiService:InternalApiKey` | Only needed for the `/api/ai/chat` and `/api/ai/summary` endpoints — see step 5 | Calls to those two endpoints fail; `/api/ai/help` doesn't need this |

`Jwt:Issuer`, `Jwt:Audience`, and `Jwt:AccessTokenMinutes` already have sane defaults in
`appsettings.json` and don't need to be set locally.

## 3. Run the database

Point `ConnectionStrings:DefaultConnection` (above) at any reachable Postgres instance,
then apply the EF Core migrations to create the schema:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

To add a new migration after changing an entity:

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
```

## 4. Run the backend

```bash
dotnet run --project src/Api
```

By default this listens on `https://localhost:7058` and `http://localhost:5017` (see
`src/Api/Properties/launchSettings.json`) with Swagger UI at `/swagger`. On startup, in
the Development environment only, the app seeds:

- Identity roles (`TeamMember`, `Manager`, `Admin`)
- A bootstrap admin account (`Seed:AdminEmail` / `Seed:AdminPassword` above)
- Demo data: 4 team members + 2 managers + 4 projects + 4 weeks of reports across every
  status (Draft/Submitted/NeedsCorrection/Approved), so the dashboard has something
  meaningful to show. Demo accounts all use the password `Password123`:
  `alice@teammanagement.local`, `bob@teammanagement.local`, `dave@teammanagement.local`,
  `erin@teammanagement.local` (TeamMember), `carol.manager@teammanagement.local`,
  `frank.manager@teammanagement.local` (Manager).

Seeding is idempotent — safe to restart the app repeatedly without duplicating data.

CORS is restricted to the origins listed under `Cors:AllowedOrigins` in
`appsettings.json` (defaults to `http://localhost:5173`, the frontend's Vite dev server
port).

## 5. (Optional) Wiring up the AI service

`/api/ai/chat` and `/api/ai/summary` (Manager/Admin-only) proxy to the separate
`team-management-ai` FastAPI service; `/api/ai/help` is open to every role but shares the
same gateway. To enable them, follow that repo's own README to run the service, then:

```bash
dotnet user-secrets set "AiService:BaseUrl" "http://localhost:8001" --project src/Api
dotnet user-secrets set "AiService:InternalApiKey" "<same value as INTERNAL_API_KEY in team-management-ai's .env>" --project src/Api
```

Without this, `/api/ai/*` calls fail with a `503` (`ExternalServiceException` — see
`AiService.PostAsync`) rather than hanging or crashing the app.

## Running tests

```bash
dotnet test
```

`tests/Api.Tests` covers role-based access control (both service-level behavior and a
reflection check that every sensitive endpoint still carries its `[Authorize(Roles=...)]`
attribute) and the report review/correction workflow, against an EF Core InMemory
database — no real Postgres instance is needed to run the suite.

## Project structure

```
src/
  Domain/          entities, enums, role constants — no dependencies on other layers
  Application/      business logic, one feature folder per resource (Auth, Users,
                    Projects, Reports, Dashboard, Ai), each with a service + DTOs +
                    FluentValidation validators
  Infrastructure/    EF Core DbContext, entity configurations, migrations, JWT issuing,
                    startup seeding
  Api/               thin controllers, composition root (Program.cs), cross-cutting
                    middleware/filters
tests/
  Api.Tests/         xUnit test suite (see "Running tests" above)
```

See [CLAUDE.md](CLAUDE.md) for more on the layering, the request pipeline
(`ValidationFilter`, `ExceptionHandlingMiddleware`), and the reports domain model.
