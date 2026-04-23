# Azen — MVP Design Document

**Version**: 2.0
**Date**: April 2026
**Stack**: .NET 8 · MSSQL 2019 · React Native
**Team**: Rajesh, Aditya, Shivesh

---

## 1. Product Overview

Azen is a mobile-first logistics coordination platform for Indian mid-sized transporters. It replaces WhatsApp-based shipment workflows with structured, traceable shipment management and centralized document storage.

**MVP Goal**: Shipment creation, actor assignment, document management (Invoice, LR, POD, etc.), and secure document sharing — faster and more reliable than WhatsApp.

**One-Line Vision**: Replace fragmented WhatsApp-based logistics operations with a structured, trackable, document-driven workflow platform.

---

## 2. Tech Stack

- **Backend**: .NET 8 Web API (Clean Architecture)
- **Database**: MSSQL Server 2019 — two databases: `AuthDb` + `AppDb`
- **ORM**: Entity Framework Core
- **Auth**: Phone OTP + JWT (access 15min, refresh 30d)
- **Storage (MVP)**: Local disk via `IStorageService` interface (swappable to Azure Blob)
- **Mobile**: React Native (iOS + Android)

---

## 3. Actors & Flexible Participation Model

### 3.1 Internal Roles (registered users)

**Transporter** — Primary user and system owner. Creates shipments, assigns fleet owners, manages documents, generates share links. Has **full override capability** on any shipment in their org — never blocked by missing actors.

**Fleet Owner** — Assigned by transporter. Assigns drivers, adds vehicle info, uploads documents on assigned shipments.

**Driver** — Assigned by fleet owner or transporter. Uploads POD and delivery documents on assigned shipments.

### 3.2 External Actors (no login, MVP)

**Consignor (Shipper)** — Sends shipment request to transporter (via WhatsApp). Views POD through share link. Future phase: registered user.

**Consignee (Receiver)** — Receives goods. Views POD through share link. Future phase: registered user.

### 3.3 Flexible Participation Rules

This is the core design principle. **The transporter is never blocked.**

**Scenario A — All actors in system (ideal flow):**
- Transporter creates shipment → assigns Fleet Owner (in-app)
- Fleet Owner assigns Driver (in-app) → Driver uploads POD
- Transporter generates share link for Consignor
- Permissions cascade: Driver uploads → Fleet Owner manages → Transporter oversees

**Scenario B — Fleet Owner / Driver NOT in system (external):**
- Transporter creates shipment → records Fleet Owner name + phone as metadata (marked "external")
- Transporter manages everything: uploads docs received via WhatsApp, advances status
- External actors can call transporter (phone number visible) to coordinate

**Scenario C — Fleet Owner / Driver in system but unavailable (app issues, logged out):**
- Same as Scenario B — transporter has full override to manage on their behalf
- Once the actor is back, they see the updated shipment state

**Transporter override rule — always permitted:**
- Create / edit shipments
- Change shipment status (any transition)
- Upload / delete documents
- Assign / reassign fleet owner and driver
- Generate / revoke share links

---

## 4. Shipment Lifecycle

### 4.1 Statuses

```
created → assigned → pod_uploaded → shared
```

| Status | Meaning |
|---|---|
| `created` | Shipment record created by transporter |
| `assigned` | Fleet owner and/or driver assigned (in system or external) |
| `pod_uploaded` | At least one POD document uploaded |
| `shared` | Share link generated for external parties |

### 4.2 Transitions

Forward-only. Skipping `assigned` is allowed (transporter self-manages).

```
created ──→ assigned ──→ pod_uploaded ──→ shared
   │                          ▲
   └──────────────────────────┘
   (transporter uploads POD directly without assigning anyone)
```

### 4.3 Who Can Trigger Transitions

| Transition | Transporter | Fleet Owner | Driver |
|---|---|---|---|
| created → assigned | ✅ assign fleet owner | — | — |
| created → pod_uploaded | ✅ self-managed upload | — | — |
| assigned → pod_uploaded | ✅ override | ✅ if no driver in system | ✅ if assigned |
| pod_uploaded → shared | ✅ generate share link | — | — |

Auto-triggers:
- Assigning a fleet owner → status becomes `assigned` (if currently `created`)
- Uploading a POD doc → status becomes `pod_uploaded` (if currently `created` or `assigned`)
- Generating a share link → status becomes `shared` (if currently `pod_uploaded`)
- Transporter can also manually advance status without the triggering action

---

## 5. Auth Flow

### 5.1 OTP + JWT Flow

```
1. POST /auth/otp/send { phone }
   → OTP sent via SMS

2. POST /auth/otp/verify { phone, otp }
   → Returns: auth_code (5min TTL) + user info + list of org memberships
   → If no orgs: user sees "Create Organisation" screen

3a. POST /auth/token/issue { auth_code, org_id }
    → Returns: access_token + refresh_token (for existing org)

3b. POST /orgs { auth_code, name, slug }
    → Creates org + transporter membership + returns access_token + refresh_token

4. POST /auth/token/refresh { refresh_token }
   → Rotate: revoke old, issue new pair

5. POST /auth/logout { refresh_token }
   → Revoke refresh token
```

### 5.2 JWT Payload

```json
{
  "sub": "user-uuid",
  "org_id": "org-uuid",
  "member_id": "org-member-uuid",
  "role": "transporter",
  "sub_role": "member",
  "iat": 1740000000,
  "exp": 1740000900
}
```

### 5.3 Middleware Chain (authenticated requests)

```
Request
  → ① verifyJWT (validate signature + expiry, extract claims) → 401 if invalid
  → ② authorize(action, resource) (ABAC policy check) → 403 if denied
  → Controller → Service → Repository
```

Public routes (`/public/*`, `/auth/*`) skip middleware.

---

## 6. Database Schema

### 6.1 Topology

```
AuthDb (shared, single instance)        AppDb (shared, single instance)
├── users                               ├── organisations
├── otp_requests                        ├── organisation_members
└── refresh_tokens                      ├── shipments
                                        ├── shipment_documents
                                        ├── share_links
                                        ├── shipment_events
                                        └── shipment_ref_sequences
```

Cross-DB reference: `organisation_members.user_id` → `AuthDb.users.id` (enforced at application level, no FK constraint).

### 6.2 AuthDb Tables

#### users
```sql
CREATE TABLE users (
    id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    phone         NVARCHAR(15)     NOT NULL UNIQUE,
    name          NVARCHAR(200)    NOT NULL DEFAULT '',
    email         NVARCHAR(255)    NULL,
    is_active     BIT              NOT NULL DEFAULT 1,
    created_at    DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at    DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);
```

#### otp_requests
```sql
CREATE TABLE otp_requests (
    id                    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    phone                 NVARCHAR(15)     NOT NULL,
    otp_hash              NVARCHAR(255)    NOT NULL,         -- bcrypt hash of 6-digit OTP
    expires_at            DATETIME2(7)     NOT NULL,         -- created_at + 10 minutes
    is_used               BIT              NOT NULL DEFAULT 0,
    attempt_count         INT              NOT NULL DEFAULT 0,
    -- Auth code: issued after successful OTP verification
    auth_code_hash        NVARCHAR(255)    NULL,
    auth_code_expires_at  DATETIME2(7)     NULL,             -- verify time + 5 minutes
    auth_code_used        BIT              NOT NULL DEFAULT 0,
    created_at            DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_otp_phone ON otp_requests (phone, is_used, expires_at);
```

#### refresh_tokens
```sql
CREATE TABLE refresh_tokens (
    id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id       UNIQUEIDENTIFIER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    org_id        UNIQUEIDENTIFIER NOT NULL,    -- AppDb.organisations ref (no FK)
    token_hash    NVARCHAR(255)    NOT NULL,    -- SHA-256 hash
    expires_at    DATETIME2(7)     NOT NULL,    -- created_at + 30 days
    revoked_at    DATETIME2(7)     NULL,        -- NULL = active
    created_at    DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_rt_user ON refresh_tokens (user_id, revoked_at);
```

### 6.3 AppDb Tables

#### organisations
```sql
CREATE TABLE organisations (
    id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    name          NVARCHAR(200)    NOT NULL,
    slug          NVARCHAR(60)     NOT NULL UNIQUE,    -- e.g. "anil-logistics"
    plan          NVARCHAR(20)     NOT NULL DEFAULT 'mvp'
                  CHECK (plan IN ('mvp', 'growth', 'enterprise')),
    is_active     BIT              NOT NULL DEFAULT 1,
    created_at    DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);
```

#### organisation_members
```sql
CREATE TABLE organisation_members (
    id                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    organisation_id   UNIQUEIDENTIFIER NOT NULL REFERENCES organisations(id),
    user_id           UNIQUEIDENTIFIER NOT NULL,    -- AuthDb.users ref (no FK)
    role              NVARCHAR(20)     NOT NULL
                      CHECK (role IN ('transporter', 'fleet_owner', 'driver')),
    sub_role          NVARCHAR(20)     NOT NULL DEFAULT 'member'
                      CHECK (sub_role IN ('member', 'manager')),
    is_active         BIT              NOT NULL DEFAULT 1,
    joined_at         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_org_user UNIQUE (organisation_id, user_id)
);

CREATE INDEX IX_orgmembers_user ON organisation_members (user_id);
CREATE INDEX IX_orgmembers_org  ON organisation_members (organisation_id, role);
```

> `sub_role = 'manager'` is only meaningful for `fleet_owner` — grants org-wide shipment visibility.

#### shipments
```sql
CREATE TABLE shipments (
    id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    organisation_id         UNIQUEIDENTIFIER NOT NULL REFERENCES organisations(id),
    reference_number        NVARCHAR(100)    NOT NULL,

    -- Consignor / Consignee (always external for MVP)
    consignor_name          NVARCHAR(200)    NULL,
    consignor_phone         NVARCHAR(15)     NULL,
    consignee_name          NVARCHAR(200)    NULL,
    consignee_phone         NVARCHAR(15)     NULL,

    -- Shipment details
    goods_description       NVARCHAR(500)    NULL,
    vehicle_number          NVARCHAR(50)     NULL,

    -- Status
    status                  NVARCHAR(20)     NOT NULL DEFAULT 'created'
                            CHECK (status IN ('created','assigned','pod_uploaded','shared')),

    -- Fleet Owner (NULL = not assigned; in_system = 0 means external metadata only)
    fleet_owner_member_id   UNIQUEIDENTIFIER NULL REFERENCES organisation_members(id),
    fleet_owner_name        NVARCHAR(200)    NULL,
    fleet_owner_phone       NVARCHAR(15)     NULL,
    fleet_owner_in_system   BIT              NOT NULL DEFAULT 0,

    -- Driver (same pattern)
    driver_member_id        UNIQUEIDENTIFIER NULL REFERENCES organisation_members(id),
    driver_name             NVARCHAR(200)    NULL,
    driver_phone            NVARCHAR(15)     NULL,
    driver_in_system        BIT              NOT NULL DEFAULT 0,

    -- Metadata (always editable)
    notes                   NVARCHAR(MAX)    NULL,

    -- Audit
    created_by              UNIQUEIDENTIFIER NOT NULL REFERENCES organisation_members(id),
    created_at              DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at              DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_shipment_ref_per_org UNIQUE (organisation_id, reference_number)
);

CREATE INDEX IX_shipments_org    ON shipments (organisation_id, status);
CREATE INDEX IX_shipments_fleet  ON shipments (fleet_owner_member_id);
CREATE INDEX IX_shipments_driver ON shipments (driver_member_id);
```

#### shipment_documents
```sql
CREATE TABLE shipment_documents (
    id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    shipment_id         UNIQUEIDENTIFIER NOT NULL REFERENCES shipments(id) ON DELETE CASCADE,
    doc_type            NVARCHAR(30)     NOT NULL
                        CHECK (doc_type IN ('pod','invoice','lr','weighbridge','eway_bill',
                                            'consignment_note','custom')),
    storage_key         NVARCHAR(500)    NOT NULL,    -- local path or blob key
    original_filename   NVARCHAR(255)    NOT NULL,
    file_size_bytes     INT              NOT NULL,
    mime_type           NVARCHAR(100)    NOT NULL
                        CHECK (mime_type IN ('image/jpeg','image/png','image/webp','application/pdf')),
    uploaded_by         UNIQUEIDENTIFIER NOT NULL REFERENCES organisation_members(id),
    uploader_role       NVARCHAR(20)     NOT NULL,
    is_deleted          BIT              NOT NULL DEFAULT 0,
    created_at          DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_docs_shipment ON shipment_documents (shipment_id, is_deleted);
```

#### share_links
```sql
CREATE TABLE share_links (
    id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    token               NVARCHAR(12)     NOT NULL UNIQUE,    -- base62 slug e.g. "xK9bP2m4Qr"
    shipment_id         UNIQUEIDENTIFIER NOT NULL REFERENCES shipments(id),
    created_by          UNIQUEIDENTIFIER NOT NULL REFERENCES organisation_members(id),
    expires_at          DATETIME2(7)     NOT NULL,            -- default: created_at + 30 days
    is_revoked          BIT              NOT NULL DEFAULT 0,
    visible_doc_types   NVARCHAR(MAX)    NOT NULL DEFAULT '[]',  -- JSON array e.g. ["pod","lr"]
    access_count        INT              NOT NULL DEFAULT 0,
    last_accessed_at    DATETIME2(7)     NULL,
    created_at          DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_sharelink_shipment ON share_links (shipment_id, is_revoked);
```

#### shipment_events
```sql
CREATE TABLE shipment_events (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    shipment_id     UNIQUEIDENTIFIER NOT NULL REFERENCES shipments(id),
    event_type      NVARCHAR(50)     NOT NULL
                    CHECK (event_type IN (
                        'shipment_created','status_changed',
                        'fleet_owner_assigned','fleet_owner_reassigned',
                        'driver_assigned','driver_reassigned',
                        'vehicle_updated',
                        'document_uploaded','document_deleted',
                        'share_link_generated','share_link_revoked',
                        'metadata_updated'
                    )),
    actor_id        UNIQUEIDENTIFIER NULL,    -- NULL for system events
    actor_role      NVARCHAR(20)     NOT NULL
                    CHECK (actor_role IN ('transporter','fleet_owner','driver','system')),
    payload         NVARCHAR(MAX)    NOT NULL DEFAULT '{}',  -- JSON
    created_at      DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Append-only. Events are NEVER updated or deleted.
CREATE INDEX IX_events_shipment ON shipment_events (shipment_id, created_at);
CREATE INDEX IX_events_actor    ON shipment_events (actor_id, created_at);
```

#### shipment_ref_sequences
```sql
CREATE TABLE shipment_ref_sequences (
    organisation_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY REFERENCES organisations(id),
    last_seq        INT              NOT NULL DEFAULT 0
);
-- Atomic increment:
-- UPDATE shipment_ref_sequences SET last_seq = last_seq + 1
-- OUTPUT inserted.last_seq WHERE organisation_id = @orgId
-- Format: SHP-{YYYY}-{seq:05d} e.g. SHP-2026-00143
```

---

## 7. API Endpoints

**Base URL**: `/api/v1`

### 7.1 Auth (no auth required)

| Method | Path | Description |
|---|---|---|
| POST | `/auth/otp/send` | Send OTP to phone |
| POST | `/auth/otp/verify` | Verify OTP, return auth_code + org list |
| POST | `/auth/token/issue` | Exchange auth_code + org_id for JWT pair |
| POST | `/auth/token/refresh` | Rotate JWT pair |
| POST | `/auth/logout` | Revoke refresh token |

**POST `/auth/otp/send`**
```jsonc
// Request
{ "phone": "+919876543210" }
// Response 200
{ "message": "OTP sent", "expires_in_seconds": 600 }
```

**POST `/auth/otp/verify`**
```jsonc
// Request
{ "phone": "+919876543210", "otp": "482910" }
// Response 200
{
  "auth_code": "random-string",   // 5 min TTL, single use
  "auth_code_expires_in": 300,
  "user": { "id": "uuid", "name": "Rajesh", "phone": "+919876543210" },
  "organisations": [
    { "org_id": "uuid", "name": "Anil Logistics", "slug": "anil-logistics", "role": "transporter" }
  ]
}
// organisations = [] means new user, show "Create Org" screen
```

**POST `/auth/token/issue`**
```jsonc
// Request
{ "auth_code": "random-string", "org_id": "uuid" }
// Response 200
{ "access_token": "eyJ...", "refresh_token": "dGhp...", "expires_in": 900 }
```

### 7.2 Organisations & Members

| Method | Path | Role | Description |
|---|---|---|---|
| POST | `/orgs` | (auth_code, no JWT) | Create org + become transporter |
| GET | `/orgs/current` | All | Get current org details |
| POST | `/orgs/current/members/invite` | Transporter | Invite member (creates user if needed) |
| GET | `/orgs/current/members` | Transporter | List org members |
| PATCH | `/orgs/current/members/:memberId` | Transporter | Update member role / deactivate |
| GET | `/users/me` | All | Get current user profile |
| PATCH | `/users/me` | All | Update own name |

**POST `/orgs`** (first-time org creation)
```jsonc
// Request
{ "auth_code": "random-string", "name": "Anil Logistics", "slug": "anil-logistics" }
// Response 201
{
  "org": { "id": "uuid", "name": "Anil Logistics", "slug": "anil-logistics" },
  "access_token": "eyJ...",
  "refresh_token": "dGhp..."
}
```

**POST `/orgs/current/members/invite`**
```jsonc
// Request
{ "phone": "+919876543210", "name": "Aditya Sharma", "role": "fleet_owner" }
// Response 201
{ "member_id": "uuid", "user_id": "uuid", "role": "fleet_owner", "is_new_user": true }
// If user doesn't exist in AuthDb, creates them automatically
```

### 7.3 Shipments

| Method | Path | Role | ABAC | Description |
|---|---|---|---|---|
| POST | `/shipments` | Transporter | org member | Create shipment |
| GET | `/shipments` | All | canView (filtered) | List shipments (role-filtered) |
| GET | `/shipments/:id` | All | canView | Get shipment detail |
| PATCH | `/shipments/:id` | Transporter | canEdit | Edit shipment fields |
| POST | `/shipments/:id/assign-fleet-owner` | Transporter | canAssign | Assign fleet owner |
| POST | `/shipments/:id/assign-driver` | Transporter, Fleet Owner | canAssignDriver | Assign driver |
| PATCH | `/shipments/:id/status` | Transporter | org member | Manually advance status |

**GET `/shipments` role-based filtering:**
```
transporter         → WHERE org_id = user.org_id
fleet_owner/member  → WHERE fleet_owner_member_id = user.member_id
fleet_owner/manager → WHERE org_id = user.org_id
driver              → WHERE driver_member_id = user.member_id
```

**POST `/shipments`**
```jsonc
// Request
{
  "reference_number": "SHP-2026-00150",    // optional, auto-generated if omitted
  "consignor_name": "Rajesh Industries",   // optional
  "consignor_phone": "+919876543210",      // optional
  "consignee_name": "Pulkit Enterprises",  // optional
  "consignee_phone": "+919876543211",      // optional
  "goods_description": "Wood pallets, 2 tons",
  "notes": "16ft truck required"
}
// Response 201
{
  "id": "uuid",
  "reference_number": "SHP-2026-00150",
  "status": "created",
  "created_at": "2026-04-15T06:00:00Z"
}
```

**POST `/shipments/:id/assign-fleet-owner`**
```jsonc
// Option A: Fleet owner is in system
{ "member_id": "uuid" }

// Option B: Fleet owner is external (not registered)
{ "name": "Aditya Transport", "phone": "+919876543210" }

// Response 200
{
  "shipment_id": "uuid",
  "fleet_owner_name": "Aditya Transport",
  "fleet_owner_in_system": false,
  "status": "assigned"
}
```

**POST `/shipments/:id/assign-driver`**
```jsonc
// Option A: Driver is in system
{ "member_id": "uuid", "vehicle_number": "MH 04 GH 7821" }

// Option B: Driver is external
{ "name": "Shivesh Kumar", "phone": "+919876543212", "vehicle_number": "MH 04 GH 7821" }
```

### 7.4 Documents

| Method | Path | Role | ABAC | Description |
|---|---|---|---|---|
| POST | `/shipments/:id/documents` | All | canUpload | Upload document (multipart) |
| GET | `/shipments/:id/documents` | All | canView | List documents |
| DELETE | `/shipments/:id/documents/:docId` | Transporter | canDelete | Soft-delete document |

**POST `/shipments/:id/documents`**
```
Content-Type: multipart/form-data
Fields:
  file       (binary)   required
  doc_type   (string)   required — pod | invoice | lr | weighbridge | eway_bill | consignment_note | custom
```

```jsonc
// Response 201
{
  "id": "uuid",
  "doc_type": "pod",
  "original_filename": "delivery_receipt.jpg",
  "file_size_bytes": 1243000,
  "uploader_role": "transporter",
  "created_at": "2026-04-15T09:30:00Z"
}
// Auto-advances status to pod_uploaded if doc_type = "pod" and status is created/assigned
```

### 7.5 Share Links

| Method | Path | Role | ABAC | Description |
|---|---|---|---|---|
| POST | `/shipments/:id/share-links` | Transporter | canGenerateShareLink | Generate share link |
| GET | `/shipments/:id/share-links` | Transporter | canView | List share links |
| DELETE | `/share-links/:linkId` | Transporter | link owner | Revoke share link |

**POST `/shipments/:id/share-links`**
```jsonc
// Request
{
  "visible_doc_types": ["pod", "lr"],    // transporter selects which docs to expose
  "expires_in_days": 30                  // optional, default 30
}
// Response 201
{
  "id": "uuid",
  "token": "xK9bP2m4Qr",
  "url": "https://api.azen.in/api/v1/public/s/xK9bP2m4Qr",
  "expires_at": "2026-05-15T06:00:00Z",
  "visible_doc_types": ["pod", "lr"]
}
```

### 7.6 Public (no auth — external party access)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/public/s/:token` | None | View shipment + selected documents |

```jsonc
// Response 200
{
  "shipment": {
    "reference_number": "SHP-2026-00150",
    "consignor_name": "Rajesh Industries",
    "vehicle_number": "MH 04 GH 7821",
    "status": "shared"
  },
  "documents": [
    {
      "id": "uuid",
      "doc_type": "pod",
      "view_url": "/files/org1/ship1/doc1.jpg?sig=...&exp=...",
      "original_filename": "delivery_receipt.jpg",
      "uploaded_at": "2026-04-15T09:30:00Z"
    }
  ],
  "link": {
    "expires_at": "2026-05-15T06:00:00Z"
  }
}
```

---

## 8. Access Control (ABAC)

All policies are pure functions evaluated by authorization middleware.

### canView(user, shipment)

```
transporter         → user.org_id == shipment.org_id
fleet_owner/member  → shipment.fleet_owner_member_id == user.member_id
fleet_owner/manager → user.org_id == shipment.org_id
driver              → shipment.driver_member_id == user.member_id
```

### canEdit(user, shipment)

```
transporter         → user.org_id == shipment.org_id (all fields)
fleet_owner         → shipment.fleet_owner_member_id == user.member_id (vehicle_number, notes only)
driver              → no
```

### canUploadDocument(user, shipment)

**Transporter always has upload access (core flexibility rule).**

```
transporter         → user.org_id == shipment.org_id   ← ALWAYS ALLOWED
fleet_owner         → shipment.fleet_owner_member_id == user.member_id
driver              → shipment.driver_member_id == user.member_id
```

### canDeleteDocument(user, shipment)

```
transporter         → user.org_id == shipment.org_id
fleet_owner / driver → no
```

### canAssignFleetOwner(user, shipment)

```
transporter         → user.org_id == shipment.org_id
```

### canAssignDriver(user, shipment)

```
transporter         → user.org_id == shipment.org_id
fleet_owner         → shipment.fleet_owner_member_id == user.member_id
```

### canGenerateShareLink(user, shipment)

```
transporter         → user.org_id == shipment.org_id AND status IN ('pod_uploaded', 'shared')
```

---

## 9. Document Storage

### 9.1 IStorageService Interface

```csharp
public interface IStorageService
{
    Task<string> UploadAsync(string orgId, string shipmentId, Stream file, string filename);
    Task<Stream> DownloadAsync(string storageKey);
    Task DeleteAsync(string storageKey);
    string GetSignedUrl(string storageKey, TimeSpan expiry);
}
```

### 9.2 Local Storage (MVP)

Files stored at: `./storage/{org_id}/{shipment_id}/{uuid}.{ext}`

Signed URLs for file serving: `HMAC-SHA256(path + expiry, FILE_SIGNING_SECRET)`
- 1 hour expiry for authenticated users
- 30 min expiry for public share link access

### 9.3 Future: Azure Blob Storage

Swap `LocalStorageService` for `AzureBlobStorageService` — same interface, zero API changes.

---

## 10. Error Response Standard

```jsonc
{
  "error": "ERROR_CODE",            // machine-readable
  "message": "Human readable text", // optional
  "field": "reference_number"       // optional, for validation errors
}
```

| Code | When |
|---|---|
| 400 | Validation error |
| 401 | Missing or invalid JWT |
| 403 | ABAC policy denied |
| 404 | Resource not found |
| 409 | Conflict (duplicate ref number, etc.) |
| 410 | Share link expired or revoked |
| 413 | File too large (max 5MB) |
| 415 | Unsupported file type |
| 429 | Rate limited |
| 500 | Unexpected server error |

---

## 11. MVP Scope

### Included

- Shipment creation with auto/manual reference numbers
- Fleet owner + driver assignment (in-system or external)
- Flexible transporter override (manage everything when actors are missing)
- Document upload: POD, Invoice, LR, weighbridge, eway_bill, consignment_note, custom
- Share link generation with configurable doc visibility
- Public share link for external parties (no login)
- OTP + JWT auth
- Org creation and member invitation
- Append-only audit trail (shipment_events)

### Deferred

- Payment tracking
- Push notifications / SMS on status changes
- Pagination / sorting / search on list endpoints
- AI-assisted shipment creation (Phase 1.5)
- WhatsApp bot integration (Phase 2)
- Consignor / Consignee as registered users (Phase 3)
- Analytics dashboard
- Load circulation / marketplace
- GPS tracking

---

## 12. Onboarding Flows

### New Organisation

1. User sends OTP → verifies → gets empty org list
2. User calls `POST /orgs { auth_code, name, slug }`
3. System creates org in AppDb + org_member record (role=transporter)
4. System creates user in AuthDb (if not exists)
5. JWT issued → user is logged in as transporter

### Invite Member

1. Transporter calls `POST /orgs/current/members/invite { phone, name, role }`
2. System checks AuthDb for existing user with that phone
3. If not exists: creates user in AuthDb (phone + name)
4. Creates organisation_members record in AppDb
5. Invited user can now login via OTP → sees org in their list
