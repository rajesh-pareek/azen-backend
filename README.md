# Azen Backend API

> **Internal project.** Confidential — not for external distribution.

Azen is a logistics coordination platform built to replace WhatsApp-based shipment workflows with structured, traceable, document-driven operations.

This repository is the .NET 8 Web API that powers the mobile and web clients.

---

## Tech stack

- **.NET 8** Web API (Clean Architecture: Api / Application / Domain / Infrastructure)
- **PostgreSQL** — one database shared by `AuthDbContext` and `AppDbContext`
- **Entity Framework Core** for persistence and migrations
- **MinIO** (S3-compatible) for document storage in local/dev environments
- **JWT + phone OTP** authentication
- **Docker / Docker Compose** for containerised dev and deployment

The full design is documented in [`docs/design/mvp-design.md`](docs/design/mvp-design.md). Read that first if you want the why behind the code.

---

## Prerequisites

You can run the project in two ways. Pick one.

**Option A — Docker (recommended for new devs).** You only need:

- Docker Desktop (Mac, Windows, or Linux)

**Option B — Native.** You need:

- .NET 8 SDK
- PostgreSQL 16
- MinIO running locally (or any S3-compatible storage)
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

---

## Quick start — Docker

This brings up the API, Postgres, and MinIO together as a connected stack.

```bash
# 1. Clone
git clone <repo-url>
cd azen-backend

# 2. Create your .env from the template
cp .env.example .env
# Edit .env and set POSTGRES_PASSWORD and JWT_SECRET to real values.

# 3. Start the stack
docker compose up
```

That's it. After the database health check passes, the API is reachable at:

```
http://localhost:5010/swagger
```

Database migrations apply automatically on first startup in development mode.

To stop:

```bash
docker compose down        # stops containers, keeps the data volumes
docker compose down -v     # also wipes the DB and storage (fresh start)
```

### What ports run where

| Service | Host port | Notes |
|---|---|---|
| API | `5000` | Hit `http://localhost:5010/swagger` |
| Postgres | `5432` | Local database used by both EF Core contexts |
| MinIO S3 API | `9010` | Programmatic uploads |
| MinIO console | `9011` | Web UI at `http://localhost:9011` to inspect uploaded files |

---

## Quick start — Native

For devs who prefer to run the app directly on their machine.

```bash
# 1. Clone
git clone <repo-url>
cd azen-backend

# 2. Make sure your local Postgres is running and reachable on
#    Host=localhost;Port=5432  (see appsettings.Development.json)

# 3. Make sure your local MinIO is running on http://localhost:9010

# 4. Restore + run
dotnet restore
dotnet run --project src/Azen.Api
```

The API binds to a local port shown in the console output (typically `https://localhost:7XXX`). Migrations apply on startup in Development mode.

### Migrations on native dev

Migrations run automatically on startup in Development. If you ever need to apply them manually (e.g. to a non-Development environment):

```bash
dotnet ef database update \
  --project src/Azen.Infrastructure \
  --startup-project src/Azen.Api \
  --context AuthDbContext

dotnet ef database update \
  --project src/Azen.Infrastructure \
  --startup-project src/Azen.Api \
  --context AppDbContext
```

To add a new migration after changing entities:

```bash
dotnet ef migrations add <Name> \
  --project src/Azen.Infrastructure \
  --startup-project src/Azen.Api \
  --context AppDbContext     # or AuthDbContext
```

---

## Configuration

All runtime config lives in `appsettings.json` and `appsettings.Development.json`. Environment variables override either at runtime, so Docker Compose and hosting providers can inject connection strings without baking secrets into the image.

The key sections:

**`ConnectionStrings`** — separate strings for `AuthDb` and `AppDb`.
For Neon, both can point to the same Postgres database. Use Neon's SSL connection string format for both env vars.

**`Jwt`** — token signing secret, issuer, audience, expiry windows.

**`FeatureFlags.UseRealSMS`** — `false` uses the console SMS service (prints OTPs to logs in dev). `true` calls a real SMS provider via `RealSmsService`.

**`SmsProvider.ApiKey`** — the API key for the real SMS provider when the flag is on.

**`Storage`** — MinIO / S3 endpoint, credentials, bucket name.

**`Cors.AllowedOrigins`** — array of origins allowed to call the API. Empty by default in `appsettings.json` (locked-down for prod); populated with localhost ports in `appsettings.Development.json`. Add your frontend origin here.

### Env var override syntax

ASP.NET Core treats `__` (double underscore) as the section separator. Examples:

```
ConnectionStrings__AppDb       →  ConnectionStrings:AppDb
Jwt__Secret                    →  Jwt:Secret
Cors__AllowedOrigins__0        →  Cors:AllowedOrigins[0]
```

Useful when overriding from `.env` or in a CI environment.

---

## Authentication flow

Phone-OTP based, JWT-issued.

```
1. POST /auth/otp/send       → OTP delivered via SMS
2. POST /auth/otp/verify     → returns auth_code + user + org list
3a. POST /auth/token/issue   → exchange auth_code + org_id for JWT pair
3b. POST /orgs               → create a new org (for first-time users)
4. POST /auth/token/refresh  → rotate the token pair
5. POST /auth/logout         → revoke refresh token
```

Detailed flow and JWT payload are in `docs/design/auth-flow-guide.md`.

---

## Roles and authorization

Three internal roles: **transporter**, **fleet_owner**, **driver**. External actors (consignor, consignee) view shipments through anonymous share links.

The full permission matrix lives in `mvp-design.md §8` and is implemented in `Azen.Infrastructure.Authorization.ShipmentAccessPolicy`. All controllers ask the policy — never check roles inline.

The transporter has full override on their own org's shipments, which is the core flexibility rule.

---

## Project structure

```
src/
  Azen.Api              HTTP entry point - controllers, middleware, Program.cs
  Azen.Application      Use cases, interfaces, DTOs, ABAC policy contracts
  Azen.Domain           Core entities and enums (no dependencies)
  Azen.Infrastructure   EF Core, DbContexts, migrations, storage, JWT, policy impl

docs/
  design/               Architecture and design documents
  srs/                  Product requirements
  plan/                 Implementation tracking

Dockerfile              Multi-stage build for the API image
docker-compose.yml      Local dev stack: api + db + minio
.env.example            Template for the .env you must create
```

---

## Shipment lifecycle

```
created → assigned → pod_uploaded → shared
```

Transitions are forward-only. The `assigned` stage can be skipped when the transporter manages everything directly (flexible participation rule — see design §3).

---

## API documentation

Swagger UI is enabled in Development at `/swagger`. JWT can be entered via the **Authorize** button in the top-right of the Swagger page; subsequent requests carry it as `Authorization: Bearer <token>`.

---

## Common dev tasks

**Reset the local stack from scratch:**

```bash
docker compose down -v
docker compose up
```

**Tail just the API logs:**

```bash
docker compose logs -f api
```

**Open a Postgres session against the containerised DB:**

```bash
docker exec -it azen-db psql -U azen -d azen
```

**Open MinIO console:**

```
http://localhost:9011
```

Sign in with the `MINIO_ACCESS_KEY` / `MINIO_SECRET_KEY` from your `.env`.

---

## Team

- Rajesh Pareek — backend
- Aditya Sharma — backend
- Shivesh Trivedi — backend

---

## Internal notes

- Real SMS provider integration is feature-flagged behind `FeatureFlags.UseRealSMS`. Keep it `false` in dev unless you're testing real delivery.
- Production deployments do **not** auto-apply migrations — schema changes go through a CI/CD step. Auto-apply is dev-only.
- This codebase is intended to stay closed-source. No public licence is granted.

Detailed engineering plans live in `docs/plan/`. Start with `docs/plan/mvp-hardening-plan.md` for current open work.
