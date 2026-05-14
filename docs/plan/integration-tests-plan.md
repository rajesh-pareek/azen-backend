# Integration Tests — Deferred Work Plan

**Status:** Deferred (May 2026)
**Owner:** Raps
**Estimated effort:** ~2 days for full coverage; ~4–6 hours for happy-path-only
**Picks up after:** Web app frontend reveals real API contract shape

---

## Why this is deferred

Backend MVP is now feature-complete (auth, shipments, documents, share links, audit trail, ABAC, validation, Docker setup).

Integration tests were originally Priority 2 in the hardening plan but have been pushed to a **buffer slot — likely a weekend** for two reasons:

1. **Frontend hasn't shaped the API contract yet.** Tests written before the web app integrates tend to ossify assumptions that turn out to be wrong, forcing a rewrite. Pragmatic order is: ship backend → wire frontend → fix the inevitable shape mismatches → *then* lock the contract with tests.

2. **Time budget.** The remaining day is better spent on web app design + tech stack + initial scaffolding. Tests are the kind of work that fits well into a focused weekend block without blocking forward progress.

---

## Objective

Prove three things, in order of importance:

1. **The three core shipment flows work end-to-end** (ideal, flexible, external).
2. **Permission rules behave correctly across roles** — driver can't see other drivers' shipments, transporter override always wins, cross-org access returns 404, etc.
3. **Error paths return correct status codes and error envelopes** — expired OTPs, revoked tokens, expired share links, invalid state transitions, validation failures.

The goal is **regression confidence**: after future changes, running the test suite reliably tells us whether the contract still holds.

---

## Scope — tiered

### Tier 1 — happy paths (must have)

Prove the system works for the documented flows. No edge cases.

- Ideal flow: transporter → assign FO in-system → FO assigns driver in-system → driver uploads POD → transporter shares.
- Flexible flow: transporter creates → skips assignment → uploads POD directly → shares.
- External flow: transporter creates → assigns external FO (name + phone) → uploads docs themselves → shares.

Assertions for each flow:

- Every step returns 2xx.
- Status progresses correctly through the state machine.
- Audit events get written for each mutation.
- Final share link resolves anonymously and returns only the visible doc types.

**Estimated effort: 4–6 hours.**

### Tier 2 — error and ABAC paths (should have)

Tests that should fail correctly. This is where real bugs live.

- **Auth flow edge cases**

  - Expired OTP rejected
  - Reused OTP rejected
  - Expired auth_code rejected
  - Revoked refresh token rejected
  - Wrong phone for given OTP rejected
  - OTP rate limiting kicks in after N attempts

- **ABAC enforcement**

  - Driver A cannot view Driver B's shipments
  - Fleet owner cannot delete documents
  - Fleet owner cannot generate share links
  - Transporter override applies to upload / delete / assign / share on any of their org's shipments
  - Cross-org access returns 404 (not 403 — existence hiding preserved)
  - Manager sub_role sees org-wide shipments (regression test for the fix landed in P3)

- **State machine**

  - Duplicate reference number returns 409
  - Invalid status transition (e.g. `created` → `shared`) returns 400
  - Skipping `assigned` via direct POD upload works (flexible flow already covers but add explicit unit-style test)

- **Share links**

  - Expired link returns 410
  - Revoked link returns 410
  - Visible doc types filter works — non-listed docs are not exposed in the public response
  - Access count increments on view

- **Validation envelope**

  - At least one test per validator confirming the standard `{ error, message, field }` 400 response shape
  - E.164 phone rejection on every phone field
  - XOR rule on assign-fleet-owner / assign-driver (rejects both modes, rejects neither mode)

**Estimated effort: +1 day on top of Tier 1.**

### Tier 3 — polish (nice to have)

- GitHub Actions workflow: spin up Docker, run `dotnet test`, report pass/fail
- Database reset between tests for isolation
- Performance smoke: assert N+1 queries haven't crept in (`EFCore.Diagnostics` query count assertions)

**Estimated effort: +0.5 day.**

---

## Setup work (one-time, before any test runs)

### New project

```
tests/Azen.IntegrationTests/
  Azen.IntegrationTests.csproj
```

Add to `Azen.sln`:

```bash
dotnet sln add tests/Azen.IntegrationTests/Azen.IntegrationTests.csproj
```

### Packages

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.AspNetCore.Mvc.Testing` — boots the API in-memory
- `Testcontainers.MsSql` — spins up a real SQL Server in Docker per test run
- `FluentAssertions` (optional, but writing readable assertions is worth the dependency)

### Program.cs shim

At the bottom of `src/Azen.Api/Program.cs`:

```csharp
public partial class Program { }
```

Required so `WebApplicationFactory<Program>` can locate the entry point from the test project.

### Fakes / fixtures

- `AzenWebAppFactory.cs` — extends `WebApplicationFactory<Program>`. Overrides DI to:

  - Point DbContexts at the Testcontainers-managed MSSQL
  - Replace `ISmsService` with `CapturingSmsService` (stores the last OTP per phone)
  - Replace `IStorageService` with `InMemoryStorageService` (dictionary-backed)

- `DatabaseResetFixture.cs` — truncates all tables between tests for isolation.
- `AuthHelper.cs` — `LoginAsTransporterAsync(phone)` and friends. Walks the full OTP → verify → token-issue flow and returns the access token.

### Strategy

Mixed approach:

- **Testcontainers MSSQL** for the three flow tests and anything touching `ShipmentRefService` (which uses SQL-specific `OUTPUT inserted.last_seq`).
- **`Microsoft.EntityFrameworkCore.InMemory`** for the unit-style ABAC policy tests where DB-specific behavior doesn't matter. Faster.

---

## File layout (proposed)

```
tests/Azen.IntegrationTests/
  Azen.IntegrationTests.csproj
  Fixtures/
    AzenWebAppFactory.cs
    DatabaseResetFixture.cs
  Helpers/
    AuthHelper.cs
    CapturingSmsService.cs
    InMemoryStorageService.cs
  Flows/
    IdealFlowTests.cs
    FlexibleFlowTests.cs
    ExternalFlowTests.cs
  Auth/
    AuthFlowTests.cs
  Authorization/
    AbacTests.cs
  Shipments/
    StateMachineTests.cs
  ShareLinks/
    ShareLinkLifecycleTests.cs
  Validation/
    ValidationEnvelopeTests.cs
```

---

## Acceptance criteria

The test suite is considered done when:

1. All Tier 1 flow tests pass against a freshly-seeded database.
2. All Tier 2 error/ABAC tests pass and have failing-test commits in git history (i.e. each test was confirmed to actually fail before the fix landed).
3. Running `dotnet test` from a clean clone with only Docker installed produces a green result in under 5 minutes.
4. CI workflow (Tier 3) runs on every PR and blocks merge on failures.

Tier 3 is optional for "done"; Tiers 1 + 2 are non-negotiable.

---

## When to do this

**Best fit:** a focused weekend block. Reasons:

- Testcontainers has a slow first run (~90 seconds boot, ~1.5 GB image pull). Interrupting flow constantly during a workday is annoying.
- Test scaffolding is a one-shot architectural task — better in one sitting than spread across daily 30-minute slices.
- After frontend integration starts, the contract may shift; doing this *after* the first frontend smoke-test run avoids rewriting the same tests.

**Trigger to start:**

- Frontend has at least one end-to-end happy path working against the real API.
- No major refactors are queued.

---

## Dependencies / Prerequisites

- Backend P1–P5 merged (already done as of this commit)
- Docker Desktop installed (for Testcontainers)
- Frontend has stabilised at least the auth + create-shipment flow

---

## Out of scope (explicitly not covered by integration tests)

- **Real SMS provider integration** — covered by manual smoke tests, not automated
- **Load / performance testing** — separate concern, will be addressed pre-production
- **Frontend E2E (Playwright/Cypress)** — that's the frontend team's responsibility, not this plan
- **Penetration testing** — separate security review, post-MVP

---

## Notes

- The `mvp-implementation-plan.md` originally put tests in week 7–8 of the timeline. This deferral is consistent with that ordering — not a step backward.
- ABAC policy is `pure` (no DB calls inside `ShipmentAccessPolicy`), so the bulk of permission testing can be **unit tests** instead of integration tests. Faster, cheaper, just as effective for the permission matrix. Reserve integration tests for the parts that need a real HTTP stack and a real DB.
