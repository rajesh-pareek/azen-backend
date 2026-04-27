# Azen — MVP Implementation Plan

**Timeline**: 8 weeks (April–June 2026)
**Team**: Rajesh (R), Aditya (A), Shivesh (S)
**Reference**: `mvp-design.md` (technical design), `mvp-srs.md` (requirements)

---

## Dev Assignment Summary

| Dev | Ownership Areas |
|---|---|
| **Rajesh (R)** | Infrastructure, EF Core, org management, document storage, deployment |
| **Aditya (A)** | Auth flow, shipment CRUD, state machine, share links |
| **Shivesh (S)** | Middleware, ABAC engine, assignments, events/audit, testing |

---

## Week 1–2: Foundation & Auth

**Goal**: Project compiles, both databases exist with all tables, auth flow works end-to-end.

### R — Project Infrastructure
- Configure EF Core with two DbContexts: `AuthDbContext` + `AppDbContext`
- Set up MSSQL connection strings in `appsettings.json`
- Create all entity models in `Azen.Domain` (see `mvp-design.md` §6)
  - AuthDb: `User`, `OtpRequest`, `RefreshToken`
  - AppDb: `Organisation`, `OrganisationMember`, `Shipment`, `ShipmentDocument`, `ShareLink`, `ShipmentEvent`, `ShipmentRefSequence`
- Run EF Core migrations for both databases
- Set up global error handling middleware (standard error response format)
- Set up Swagger/OpenAPI

### A — Auth Flow (OTP + JWT)
- `OtpService`: generate OTP, bcrypt hash, store, mock SMS provider for dev
- `POST /auth/otp/send` — with rate limiting
- `POST /auth/otp/verify` — return auth_code + user + org list
- `POST /auth/token/issue` — validate auth_code, issue JWT pair
- `POST /auth/token/refresh` — rotate tokens
- `POST /auth/logout` — revoke refresh token

### S — Middleware & Base Controllers
- `JwtAuthMiddleware` — validate Bearer token, extract claims to HttpContext
- `AuthorizationFilter` — base for ABAC policy checks
- `GET /users/me` and `PATCH /users/me`
- Integration tests for full auth flow

### Week 1–2 Deliverable
A developer can: send OTP → verify → get JWT → call authenticated endpoints.

---

## Week 3–4: Org Management & Shipment CRUD

**Goal**: Transporters can create orgs, invite members, create/edit shipments, assign fleet owners and drivers.

### R — Organisation & Member Management
- `POST /orgs` — create org + first transporter (uses auth_code)
- `POST /orgs/current/members/invite` — invite by phone (auto-creates user if needed)
- `GET /orgs/current/members`, `PATCH /orgs/current/members/:memberId`
- `GET /orgs/current`
- Reference number generation service (atomic increment via `OUTPUT` clause)

### A — Shipment CRUD & State Machine
- `POST /shipments` — create (auto-generate ref number if omitted)
- `GET /shipments` — role-based filtering (transporter sees all, fleet owner sees assigned, driver sees assigned)
- `GET /shipments/:id` — full detail with documents
- `PATCH /shipments/:id` — edit fields
- `PATCH /shipments/:id/status` — manual status advance
- `ShipmentStateMachine` — validate forward-only transitions, allow `assigned` skip

### S — Assignment Endpoints & ABAC Engine
- ABAC policy engine (pure functions): `canView`, `canEdit`, `canUploadDocument`, `canDeleteDocument`, `canAssignFleetOwner`, `canAssignDriver`, `canGenerateShareLink`
- Key rule: **transporter always has full access within their org**
- `POST /shipments/:id/assign-fleet-owner` — accept in-system `member_id` OR external `name` + `phone`
- `POST /shipments/:id/assign-driver` — same pattern
- Auto-advance status on assignment, log events

### Week 3–4 Deliverable
Full shipment lifecycle works: create → assign fleet owner (in-system or external) → assign driver → status advances. ABAC enforces permissions.

---

## Week 5–6: Documents & Share Links

**Goal**: Users can upload/view documents, transporters can generate share links, external parties can access shared documents.

### R — Document Upload & Storage
- Define `IStorageService` interface in `Azen.Application`
- Implement `LocalStorageService` in `Azen.Infrastructure` (path: `./storage/{org_id}/{shipment_id}/{uuid}.{ext}`)
- Signed URL generation (HMAC-SHA256)
- `POST /shipments/:id/documents` — multipart upload with validation (≤5MB, MIME allowlist)
- `GET /shipments/:id/documents` — list active docs
- `DELETE /shipments/:id/documents/:docId` — soft delete
- File serving endpoint with signed URL validation
- Auto-advance to `pod_uploaded` on POD upload

### A — Share Links
- `ShareLinkService` — Base62 token generation (10 chars)
- `POST /shipments/:id/share-links` — generate link, select visible doc types
- `GET /shipments/:id/share-links` — list links
- `DELETE /share-links/:linkId` — revoke
- `GET /public/s/:token` — public endpoint (no auth), resolve token, filter docs, return signed URLs, track access count
- Auto-advance to `shared` on link generation

### S — Shipment Events & Audit
- `ShipmentEventService` — append-only event writer
- Wire events into all state changes, assignments, doc uploads, link operations
- `GET /shipments/:id/events` — audit trail (transporter only)
- Integration tests for three key flows:
  - **Ideal**: create → assign fleet owner → assign driver → driver uploads POD → share
  - **Flexible**: create → transporter uploads POD directly → share (skip assigned)
  - **External**: create → assign external fleet owner → transporter uploads docs → share

### Week 5–6 Deliverable
Complete MVP flow works end-to-end. Documents upload, share links resolve, public access works.

---

## Week 7–8: Polish, Testing & Deploy Prep

**Goal**: Production-ready API with validation, error handling, documentation, and deployment setup.

### All — Testing & Validation
- Integration tests for all API endpoints
- ABAC policy tests: verify transporter override in all scenarios
- Flexible participation tests: external fleet owner/driver paths
- Edge cases: duplicate ref numbers, expired OTPs, revoked tokens, expired share links
- Validate error responses match standard format

### R — Infrastructure & Deployment
- Configure CORS for React Native app
- Add request logging
- Swagger with JWT auth support
- Docker setup for local team use
- Update README with local dev setup

### A — Data Validation & Edge Cases
- FluentValidation for all request DTOs
- Phone number format validation (E.164)
- File type and size validation
- Concurrent assignment conflict handling

### S — Documentation & Handoff
- Update README: setup instructions, API overview, project structure
- Verify Swagger descriptions
- Create Postman collection for manual API testing
- Final review: `mvp-design.md` vs actual implementation — update if diverged

### Week 7–8 Deliverable
API is tested, documented, and deployable. Postman collection ready for frontend team.

---

## Key Dependencies

```
Week 1–2 (Foundation + Auth)
    ↓ entities + auth must complete first
Week 3–4 (Org + Shipments + ABAC)
    ↓ ABAC engine needed for doc upload & share links
    ↓ IStorageService needed for public share link endpoint
Week 5–6 (Documents + Share Links + Events)
    ↓
Week 7–8 (Polish + Testing + Deploy)
```

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Cross-DB queries (AuthDb ↔ AppDb) | No SQL joins across databases, potential latency | Handle at application layer; cache user lookups where possible |
| SMS provider integration | Can't test real OTP without provider | Use mock/console OTP for dev; integrate real provider (MSG91/Twilio) before user testing |
| Local file storage | Files lost if disk is wiped; not shared between devs | Acceptable for MVP dev; swap to Azure Blob before any real deployment |
| 8-week timeline is tight | Features may slip | Prioritize: auth → shipments → documents → share links. Polish can extend into week 9 if needed |

---

## Feature Priority (if timeline slips)

Must-have (ship without these = no MVP):
1. Auth (OTP + JWT)
2. Org creation + member invite
3. Shipment CRUD + state machine
4. Fleet owner / driver assignment (in-system + external)
5. Document upload (transporter override always works)
6. Share link generation + public access

Nice-to-have (can ship MVP without):
- Audit trail (`shipment_events`)
- Share link revocation
- Member deactivation
- Manual status advance endpoint
- FluentValidation (can use basic model validation initially)
