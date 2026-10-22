# Franc API

ASP.NET Core 8 backend for Franc, a career-guidance platform. It runs the
self-assessment questionnaire, mock interviews with video answers and evaluator
scoring, job search and comparison tools, a gamified quiz, and an admin
analytics dashboard.

## Stack

| | |
|---|---|
| Runtime | .NET 8 (ASP.NET Core Web API) |
| Database | PostgreSQL via EF Core 8 (Npgsql) |
| Auth | JWT bearer, HMAC-SHA256, role claim (`User` / `Admin`) |
| Storage | Azure Blob Storage (interview videos, resumes, generated reports) |
| Email | MailKit over SMTP |
| Observability | Sentry (errors + tracing), NLog (rolling file logs) |
| Documents | ClosedXML for Excel, DocxTemplater for Word reports |

## Running locally

Requires the .NET 8 SDK and a reachable PostgreSQL instance.

```bash
cp appsettings.example.json appsettings.json
```

Fill in `appsettings.json` — at minimum `ConnectionStrings:DefaultConnection`
and `AppSettings:Token` (any 32+ byte random string for local use). The file is
gitignored and must never be committed.

Restore the local `dotnet-ef` tool, apply the schema, and start the API:

```bash
dotnet tool restore && dotnet ef database update && dotnet run
```

Swagger UI is served at `https://localhost:7022/swagger` in Development only.
`GET /health` returns 200 when the database is reachable and 503 when it is not.

## Layout

```
Controllers/   HTTP surface — routing, authorization, model binding
Services/      business logic, one service per feature area
Interfaces/    service contracts (constructor-injected, registered in Program.cs)
Data/          DataContext — EF Core mappings and DbSets
Models/        persisted entities
Dto/           request and response shapes
Middlewares/   global exception handler → RFC 7807 ProblemDetails
Extensions/    cross-cutting setup and ClaimsPrincipal helpers
Helpers/       stateless utilities (scoring, Excel export, Swagger filters)
Migrations/    EF Core migration history
```

Controllers stay thin: they resolve the caller, delegate to a service, and shape
the response. Services own transactions and business rules and never touch
`HttpContext`.

## API surface

| Prefix | Area |
|---|---|
| `/api/users` | signup, verification, sign-in, password reset, admin user CRUD |
| `/api/sds` | self-assessment questionnaire, Holland scoring, Excel export |
| `/api/evaluation` | mock interview scoring, Word report generation, attempt quota |
| `/api/blob` | video and document upload, question/major catalogue |
| `/api/game` | quiz sessions, hints, abilities, scoring |
| `/api/jobcomparison` | weighted job scorecards and Excel export |
| `/api/jobsearch`, `/api/majorskills` | search tools with usage logging |
| `/api/admin/analytics` | admin dashboard aggregates (Admin role only) |

## Authorization model

Two roles: `User` and `Admin`.

The acting user is always derived from the `NameIdentifier` claim on the
validated JWT — never from a client-supplied `userId`. `ClaimsPrincipalExtensions`
provides the three helpers every controller uses:

- `User.GetUserId()` — the caller's id
- `User.IsAdmin()` — role check
- `User.ResolveAccessibleUserId(requestedId)` — returns the caller's own id, or
  `requestedId` when the caller is an Admin, and otherwise throws
  `UnauthorizedAccessException` (surfaced as 401 by the global handler)

That last helper is what lets the admin dashboard query any user's records while
keeping ordinary users scoped to their own.

## Error handling

`GlobalExceptionMiddleware` converts unhandled exceptions into `ProblemDetails`
with a `traceId`. Exception types map to status codes:

| Exception | Status |
|---|---|
| `ArgumentException`, `InvalidOperationException` | 400 |
| `UnauthorizedAccessException` | 401 |
| `KeyNotFoundException`, `FileNotFoundException` | 404 |
| anything else | 500 |

Stack traces and exception messages are included in Development only; production
responses carry a generic message plus the `traceId` to correlate with Sentry.

## Configuration

Settings resolve from `appsettings.json`, then environment variables (which win).
In Azure App Service the nested keys use double underscores — `AppSettings__Token`,
`ConnectionStrings__DefaultConnection`, `Sentry__TracesSampleRate`.

The app refuses to start if `AppSettings:Token` is missing rather than falling
back to a default signing key.

### Analytics

```json
"Analytics": {
  "UseActivityEvents": false,
  "EnableResponseCache": true,
  "CacheDurationDays": 1
}
```

`UseActivityEvents: false` (the default) reads admin analytics from the source
tables. Setting it to `true` reads from the denormalised `ActivityEvents` table,
which is faster but only correct once that table has been backfilled.

### Upload limits

`UploadSecurityOptions` caps request body size and per-category file size, and
`FileUploadSecurityService` validates content type and extension on every upload.
Defaults live in `Options/UploadSecurityOptions.cs` and are overridable under an
`UploadSecurity` configuration section.

## Deployment

`.github/workflows/deployment_franc-api.yml` builds and publishes to the
`franc-api` Azure Web App on every push to `deployment`. Secrets come from Azure
Application Settings, not from the repository.

Run `dotnet ef database update` against the target database before deploying code
that includes a new migration.
