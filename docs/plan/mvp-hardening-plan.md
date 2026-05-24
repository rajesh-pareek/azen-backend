# MVP Hardening Plan — Azen Backend

**Created:** 2026-05-12
**Owner:** Raps
**Mode:** Claude lists the files & changes, Raps writes the code.

Five priorities, in order. Don't start P2 until P1 is merged.

---

## Priority 1 — ShipmentEvents Audit Trail

**Goal:** Every state-changing mutation appends a row to `shipment_events`. Transporter can query the trail.

### Files to CREATE

| Path | Purpose |
|---|---|
| `src/Azen.Application/Interfaces/IShipmentEventService.cs` | Service contract. One method: `Task LogAsync(Guid shipmentId, ShipmentEventType type, Guid? actorMemberId, string actorRole, object? payload = null, CancellationToken ct = default)` |
| `src/Azen.Domain/Entities/App/ShipmentEventType.cs` (or `Enums/`) | Strongly-typed enum mirroring the CHECK constraint values. Avoids stringly-typed bugs at call sites. |
| `src/Azen.Infrastructure/Services/ShipmentEventService.cs` | Implementation: serialises payload to JSON, appends to `AppDbContext.ShipmentEvents`, calls `SaveChangesAsync`. No update/delete logic — append-only. |

### Files to UPDATE

| Path | Change |
|---|---|
| `src/Azen.Infrastructure/DependencyInjection.cs` | Register `IShipmentEventService` → `ShipmentEventService` (Scoped — same lifetime as DbContext). |
| `src/Azen.Api/Controllers/ShipmentsController.cs` | Inject `IShipmentEventService`. Log events at: **create** (line ~34, `shipment_created`), **update** (line ~165, `metadata_updated`), **assign-fleet-owner** (line ~203, `fleet_owner_assigned` or `…_reassigned` depending on prior state), **assign-driver** (line ~259, `driver_assigned`/`…_reassigned`), **status change** (line ~319, `status_changed`). For auto-status-advances inside assign endpoints, log a separate `status_changed` event too. |
| `src/Azen.Api/Controllers/DocumentsController.cs` | Inject service. Log at: **upload** (line ~43, `document_uploaded`), **delete** (line ~167, `document_deleted`). If POD upload auto-advances status, log a `status_changed` event as well. |
| `src/Azen.Api/Controllers/ShareLinksController.cs` | Inject service. Log at: **create** (line ~43, `share_link_generated` + `status_changed` if auto-advance fires), **revoke** (line ~122, `share_link_revoked`). |

### NEW endpoint

Add to `ShipmentsController.cs`:

```
GET /api/v1/shipments/{id}/events  →  transporter-only, returns events ordered by created_at desc
```

### Design notes / decisions you'll need to make

1. **Transactional write:** Should event logging be in the *same* `SaveChangesAsync` as the mutation, or a follow-up? Recommend: same `SaveChanges` (call `service.LogAsync` before `SaveChanges` so it adds to the change-tracker; one transaction). Otherwise you can have a mutation succeed and the audit row fail.
2. **Actor role in JWT:** Your JWT already carries `role` + `member_id`. Pull both via `HttpContext.User`. Add a small extension method on `ClaimsPrincipal` (e.g. `GetMemberId()`, `GetRole()`) in `src/Azen.Api/Authorization/ClaimsExtensions.cs` so controllers stop digging through claims by string.
3. **Payload shape:** Keep it small and consistent. Examples:
   - `status_changed`: `{ "from": "created", "to": "assigned" }`
   - `fleet_owner_assigned`: `{ "in_system": true, "member_id": "…", "name": "…" }`
   - `document_uploaded`: `{ "doc_id": "…", "doc_type": "pod", "filename": "…" }`

---

## Priority 2 — Integration Tests (Ideal / Flexible / External)

**Goal:** Three end-to-end flow tests prove flexible-participation rules work. Plus seed coverage for auth + ABAC.

### Files to CREATE

| Path | Purpose |
|---|---|
| `tests/Azen.IntegrationTests/Azen.IntegrationTests.csproj` | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.PostgreSql` (recommended) or `Microsoft.EntityFrameworkCore.InMemory` (faster but less faithful). |
| `tests/Azen.IntegrationTests/Fixtures/AzenWebAppFactory.cs` | `WebApplicationFactory<Program>` that swaps real DbContexts for test ones, swaps `ISmsService` for a capturing fake, swaps `IStorageService` for an in-memory fake. |
| `tests/Azen.IntegrationTests/Fixtures/DatabaseResetFixture.cs` | Resets both DBs between tests (truncate or recreate). |
| `tests/Azen.IntegrationTests/Helpers/AuthHelper.cs` | `Task<string> LoginAsTransporterAsync(...)`, `…AsFleetOwnerAsync`, `…AsDriverAsync`. Calls OTP send → verify → token issue, returns access token. |
| `tests/Azen.IntegrationTests/Helpers/FakeSmsService.cs` | Captures last OTP per phone so tests can read it without parsing logs. |
| `tests/Azen.IntegrationTests/Helpers/InMemoryStorageService.cs` | `IStorageService` fake — dictionary-backed, returns fake signed URLs. |
| `tests/Azen.IntegrationTests/Flows/IdealFlowTests.cs` | Transporter creates → assigns FO in-system → FO assigns driver in-system → driver uploads POD → transporter shares. Assert: status transitions, events logged, share link resolves anonymously. |
| `tests/Azen.IntegrationTests/Flows/FlexibleFlowTests.cs` | Transporter creates → uploads POD directly (skip assigned) → shares. Assert: state machine allows skip, status hits `pod_uploaded` then `shared`. |
| `tests/Azen.IntegrationTests/Flows/ExternalFlowTests.cs` | Transporter creates → assigns external FO (name+phone, no member_id) → uploads docs themselves → shares. Assert: `fleet_owner_in_system=false`, no member record created, public link works. |
| `tests/Azen.IntegrationTests/Auth/AuthFlowTests.cs` | OTP rate limit, expired OTP, expired auth_code, refresh rotation, revoked refresh token rejection. |
| `tests/Azen.IntegrationTests/Authorization/AbacTests.cs` | Driver can't see other drivers' shipments. Fleet owner can't delete docs. Transporter override works on every endpoint. |

### Files to UPDATE

| Path | Change |
|---|---|
| `azen-backend.sln` | `dotnet sln add tests/Azen.IntegrationTests/Azen.IntegrationTests.csproj`. |
| `src/Azen.Api/Program.cs` | Add `public partial class Program { }` at the bottom — required so `WebApplicationFactory<Program>` can see the entry point. |

### Decisions you'll need to make

1. **Testcontainers vs InMemory.** Testcontainers spins a real Postgres in Docker per run (slow start, faithful). InMemory is fast but doesn't enforce unique constraints or relational behavior. Recommend Testcontainers for these three flow tests; switch to InMemory for unit-style policy tests later.
2. **JWT signing in tests:** Use the same secret as `appsettings.Test.json`. Easier than mocking `IJwtService`.

---

## Priority 3 — Extract ABAC into a Policy Service

**Goal:** Pull the 14 inline role checks out of controllers, into a single testable service.

### Files to CREATE

| Path | Purpose |
|---|---|
| `src/Azen.Application/Authorization/ShipmentAccessContext.cs` | Plain record carrying the caller's claims: `Guid UserId, Guid OrgId, Guid MemberId, string Role, string SubRole`. Built once per request from `ClaimsPrincipal`. |
| `src/Azen.Application/Authorization/IShipmentAccessPolicy.cs` | Interface with the methods the design doc specifies: `bool CanView(ShipmentAccessContext ctx, Shipment s)`, `CanEdit`, `CanUploadDocument`, `CanDeleteDocument`, `CanAssignFleetOwner`, `CanAssignDriver`, `CanGenerateShareLink`. Also a `IQueryable<Shipment> FilterVisible(IQueryable<Shipment>, ctx)` for list filtering. |
| `src/Azen.Infrastructure/Authorization/ShipmentAccessPolicy.cs` | Pure-function implementation matching `mvp-design.md §8` exactly. No DB calls inside — caller hands it the loaded shipment. |
| `src/Azen.Api/Authorization/ClaimsExtensions.cs` | `ToShipmentAccessContext(this ClaimsPrincipal user)` so controllers don't repeat claim-parsing boilerplate. |

### Files to UPDATE

| Path | Change |
|---|---|
| `src/Azen.Infrastructure/DependencyInjection.cs` | Register `IShipmentAccessPolicy` as Singleton (it's pure). |
| `src/Azen.Api/Controllers/ShipmentsController.cs` | Replace inline checks at lines 37, 94–97, 135–139, 177–195, 208, 270–275 with `_policy.CanX(ctx, shipment)` calls. Replace list filter (94–97) with `_policy.FilterVisible(query, ctx)`. |
| `src/Azen.Api/Controllers/DocumentsController.cs` | Replace inline checks at 79–86, 142–147, 173. |
| `src/Azen.Api/Controllers/ShareLinksController.cs` | Replace inline checks at 49, 96–97, 127–128. Note: `CanGenerateShareLink` also enforces `status IN ('pod_uploaded','shared')` per design — make sure the policy enforces this, not the controller. |

### Decisions you'll need to make

1. **Policy returns bool vs throws:** Recommend bool — controllers convert to `Forbid()` themselves. Keeps policy pure and testable.
2. **Where does the "transporter override" live?** Inside each policy method as the first branch. Don't sprinkle it at call sites.

---

## Priority 4 — CORS, Dockerfile, README

**Goal:** Frontend team can hit the API from their dev machine. New devs can `docker compose up` and have a working stack. README isn't a stub.

### Files to CREATE

| Path | Purpose |
|---|---|
| `Dockerfile` | Multi-stage: `mcr.microsoft.com/dotnet/sdk:8.0` for build, `mcr.microsoft.com/dotnet/aspnet:8.0` for runtime. Final image runs `Azen.Api.dll`, exposes port 8080. |
| `.dockerignore` | Exclude `bin/`, `obj/`, `.vs/`, `.idea/`, `*.user`, `docs/`, `tests/`. |
| `docker-compose.yml` | Local Postgres, MinIO, and API services wired through environment variables. |

### Files to UPDATE

| Path | Change |
|---|---|
| `src/Azen.Api/Program.cs` | Add `builder.Services.AddCors(...)` reading `Cors:AllowedOrigins` from config. Add `app.UseCors("Default")` **before** `UseAuthentication`. |
| `src/Azen.Api/appsettings.json` | Add `"Cors": { "AllowedOrigins": [ "http://localhost:5173", "http://localhost:8081" ] }` (Vite + RN Metro defaults — adjust). |
| `src/Azen.Api/appsettings.Development.json` | Looser CORS for dev (`"*"` if you must, but prefer explicit list). |
| `README.md` | Sections: Overview, Architecture (link to `mvp-design.md`), Local setup (docker compose path AND bare-metal path), Running migrations, Configuration reference (every env var), Running tests, API docs (Swagger URL), Project structure. |

### Decisions you'll need to make

1. **CORS credentials:** If frontend sends cookies, you need `AllowCredentials()` AND explicit origins (no `*`). JWT in Authorization header doesn't need it. Default to no credentials.
2. **Postgres in Docker:** Use the native `postgres:16-alpine` image locally so Apple Silicon machines run the database without emulation overhead.

---

## Priority 5 — FluentValidation + E.164 Phone

**Goal:** DTO validation lives in one place per DTO, all phone fields validated against E.164, all error responses match the standard format from `mvp-design.md §10`.

### Files to CREATE

All under `src/Azen.Application/Validation/`:

| Path | Validates |
|---|---|
| `Common/PhoneNumberValidator.cs` | Reusable `RuleBuilderExtensions` — regex `^\+[1-9]\d{1,14}$` (E.164). |
| `Common/SlugValidator.cs` | Org slug rules: lowercase, alphanumeric + hyphens, 3–60 chars. |
| `Auth/SendOtpRequestValidator.cs` | Phone required + E.164. |
| `Auth/VerifyOtpRequestValidator.cs` | Phone E.164, OTP exactly 6 digits. |
| `Auth/TokenRequestValidator.cs` | AuthCode required, OrgId not empty. |
| `Auth/RefreshTokenRequestValidator.cs` | RefreshToken required. |
| `Auth/CreateOrgRequestValidator.cs` | AuthCode required, Name 1–200 chars, Slug rules. |
| `Auth/UpdateMeRequestValidator.cs` | Name 1–200 chars, Email optional + email format. |
| `App/CreateShipmentRequestValidator.cs` | RefNumber optional max 100, consignor/consignee phones E.164 if present, goods desc max 500. |
| `App/UpdateShipmentRequestValidator.cs` | Same field rules as create, all optional. |
| `App/AssignFleetOwnerRequestValidator.cs` | **Either** MemberId **or** (Name + Phone) — write a custom `Must` rule. Phone E.164. |
| `App/AssignDriverRequestValidator.cs` | Same XOR rule. VehicleNumber max 50. |
| `App/UpdateStatusRequestValidator.cs` | Status ∈ allowed enum. |
| `App/CreateShareLinkRequestValidator.cs` | VisibleDocTypes non-empty, each in allowlist, ExpiresInDays 1–90. |
| `App/InviteMemberRequestValidator.cs` | Phone E.164, Name 1–200, Role ∈ {transporter, fleet_owner, driver}. |

### Files to UPDATE

| Path | Change |
|---|---|
| `src/Azen.Application/Azen.Application.csproj` | Add `<PackageReference Include="FluentValidation" Version="11.*" />`. |
| `src/Azen.Api/Azen.Api.csproj` | Add `<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />`. |
| `src/Azen.Api/Program.cs` | `builder.Services.AddValidatorsFromAssembly(typeof(SendOtpRequestValidator).Assembly);` Add a model-state filter or action filter that returns the standard 400 error shape. |
| `src/Azen.Api/Middlewares/ErrorHandlingMiddleware.cs` | Catch `FluentValidation.ValidationException` and convert to the standard `{ error, message, field }` 400 response (use the *first* field error; matches design doc). |
| Every DTO (`src/Azen.Application/DTOs/...`) | No code change needed — validators reference DTOs externally. Just remove any inline null-checks the controllers were doing once validators are wired. |

### Decisions you'll need to make

1. **Auto-validation vs manual:** FluentValidation v11 removed the AspNetCore auto-binding package. Either inject `IValidator<T>` into each controller and call `await _validator.ValidateAndThrowAsync(dto)`, or write a tiny `[ValidateModel]` action filter. Recommend the action filter — controllers stay clean.
2. **Phone normalisation:** Validate E.164 format but also normalise (strip spaces/dashes) **before** validation, in a binder or in the DTO setter. Otherwise users typing `+91 98765 43210` get rejected.

---

## Suggested order within Priority 1 (start here)

1. Add `ShipmentEventType` enum.
2. Add `IShipmentEventService` interface.
3. Add `ShipmentEventService` implementation.
4. Register in DI.
5. Wire `ShipmentsController` (5 mutations).
6. Wire `DocumentsController` (2 mutations).
7. Wire `ShareLinksController` (2 mutations).
8. Add `GET /shipments/{id}/events` endpoint.
9. Smoke test manually via Swagger.
