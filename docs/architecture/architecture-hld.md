# High-Level Architecture
**Project:** SC R&DT · POD Tracker
**Version:** 1.0 — Based on finalized decisions (Feb 2026)

---

## Finalized Decision Summary

| # | Decision | Chosen |
|---|---|---|
| Q1 | Assignment changes | **C — Event log** (immutable, append-only) |
| Q2 | Shipment lock | **C — Field-level** (core locks at `Shared`, metadata always editable) |
| Q3 | Documents | **B — Multiple docs with types** (`doc_type` enum) |
| Q4 | Broker scope | **B — Configurable per link** (transporter selects visible docs) |
| Q5 | Attachment permissions | **Custom rule** — cascades by assignment state (see ABAC) |
| Q6 | Trucker visibility | **C — Manager override** (`trucker` + `trucker_manager` sub-role) |
| Q7 | Shipment number | **C — User-defined with fallback**, unique per org |
| Q8 | Org model | **B — Org + Users** |
| Q9 | Broker | **A — Anonymous external** (no login required) |
| Q10 | Share links | **B — Separate `share_links` table** (+ Redis caching later) |
| Q11 | Audit log | **B — Append-only `shipment_events` table** |
| Q12 | Storage | **B — Local disk, MVP** via `IStorageService` interface (S3-switchable) |
| Q13 | Compression | **B — Client-side** (API hard-rejects if over size limit) |
| Q14 | Schema readiness | **Future-ready now** (`shipment_documents` table from day one) |
| Q15 | Auth | **C — Phone OTP + JWT** (access 15m + refresh 30d) |
| Q16 | Token entropy | **B — Base62 slug** (10 chars, e.g. `xK9bP2m4`) |
| Q17 | Access control | **C — ABAC** (attribute-based policy engine) |
| Q18 | Volume | **MVP scale** → horizontal scaling path defined |
| Q19 | Multi-tenancy | **C — DB-per-tenant** (master registry + per-org Postgres) |
| Q20 | Concurrent uploads | **100–1,000** → presigned URL upload flow (ready for S3 switch) |
| Q21 | WhatsApp | **Deeplink only** — `wa://send?text=<link>`, zero API integration |
| Q22 | AI | **Deferred** — design leaves async worker hook in Document Service |
| Q23 | Events | **Direct function calls** (MVP) |

> **⚠ Architecture Note — Q19 vs Q18:**
> DB-per-tenant (Q19: C) at MVP scale (Q18) adds operational overhead —
> each new org requires a provisioned Postgres instance and migrations must run per-tenant.
> Mitigate by using a migration runner that iterates all registered tenant DBs.
> This choice provides maximum data isolation and is correct long-term. Accept the MVP overhead.

---

## 1. High-Level Architecture Diagram

```mermaid
graph TB
    subgraph Clients["Client Layer"]
        MOB["📱 Mobile App\nReact Native\nTransporter · Trucker · Driver"]
        WEB["🌐 Browser\nBroker Public View\nNo login required"]
    end

    subgraph Gateway["API Gateway"]
        GW["API Gateway\nSSL Termination\nRate Limiting · Request Validation"]
    end

    subgraph API["REST API Server (Node.js)"]
        RT["Router"]
        MW["Middleware Stack\n① JWT Verify\n② Tenant Resolve\n③ ABAC Authorize"]
        BL["Business Logic Layer\nShipment · Document · ShareLink · Auth"]
    end

    subgraph Auth["Auth Services"]
        OTP["OTP Service"]
        JWTS["JWT Service\nAccess Token 15min\nRefresh Token 30d"]
        SMS["SMS Gateway\nMSG91 / Twilio"]
    end

    subgraph Storage["Storage Layer"]
        ISS["IStorageService\nAbstraction Interface\n.upload() .getUrl() .delete()"]
        LS["LocalStorageService\nMVP — Local Disk\n/storage/org_id/ship_id/"]
        S3["S3StorageService\nFuture — AWS S3 / R2\nPresigned URLs"]
    end

    subgraph DataLayer["Data Layer"]
        DBR["Tenant DB Router\nResolves org_id → DB connection"]
        MREG[("Master Registry DB\nPostgres\norg_id · db_host · db_name")]
        T1[("Org 1 DB\nPostgres")]
        T2[("Org 2 DB\nPostgres")]
        TN[("Org N DB\nPostgres")]
    end

    subgraph Mobile["Mobile Platform — No API"]
        WALINK["WhatsApp Deep Link\nwa://send?text=POD link"]
        OSHARE["OS Share Sheet\niOS / Android\nShare doc from WA → App"]
    end

    MOB -->|HTTPS| GW
    WEB -->|HTTPS| GW
    GW --> RT
    RT --> MW
    MW --> BL
    BL --> OTP
    BL --> JWTS
    OTP --> SMS
    BL --> ISS
    ISS --> LS
    ISS -.->|"Swap anytime\nzero code change"| S3
    BL --> DBR
    DBR --> MREG
    DBR --> T1
    DBR --> T2
    DBR --> TN
    MOB -.->|"No API call"| WALINK
    MOB -.->|"OS-level"| OSHARE
```

---

## 2. Component Diagram — API Server Internals

```mermaid
graph LR
    subgraph Router["Router Layer"]
        R1["/auth/*"]
        R2["/shipments/*"]
        R3["/documents/*"]
        R4["/share-links/*"]
        R5["/public/s/:token"]
    end

    subgraph Middleware["Middleware Stack (ordered)"]
        M1["① verifyJWT\nDecode + validate access token\nSkip for /public routes"]
        M2["② resolveTenant\nLook up org_id → DB connection\nInject db client into request"]
        M3["③ authorize\nABAC policy check\nuser + resource + action"]
    end

    subgraph Controllers["Controllers"]
        AC["AuthController"]
        SC["ShipmentController"]
        DC["DocumentController"]
        SLC["ShareLinkController"]
        PC["PublicController\n(no auth)"]
    end

    subgraph Services["Services"]
        AS["AuthService\nOTP generate/verify\nJWT sign/verify\nRefresh token rotate"]
        SS["ShipmentService\nCRUD · State machine\nRef number generation"]
        DS["DocumentService\nUpload · Permission check\nIStorageService call"]
        SLS["ShareLinkService\nBase62 token gen\nExpiry · Revoke\nDoc visibility config"]
        ES["EventService\nAppend-only event writer\nNo updates, only INSERT"]
    end

    subgraph DAL["Data Access Layer (Repositories)"]
        UR["UserRepository"]
        SHR["ShipmentRepository"]
        DOCR["DocumentRepository"]
        SLNKR["ShareLinkRepository"]
        EVTR["EventRepository"]
        OTR["OTPRepository"]
        RTR["RefreshTokenRepository"]
    end

    subgraph Infra["Infrastructure"]
        ABACENG["ABACPolicyEngine\npolicies/shipment.policy.js\npolicies/document.policy.js\npolicies/sharelink.policy.js"]
        DBROUTER["TenantDBRouter\nmaster DB lookup\nconnection pool per org"]
        STORABS["IStorageService\nLocalStorageService (MVP)\nS3StorageService (future)"]
    end

    R1 --> M1 --> AC
    R2 --> M1 --> M2 --> M3 --> SC
    R3 --> M1 --> M2 --> M3 --> DC
    R4 --> M1 --> M2 --> M3 --> SLC
    R5 --> PC

    AC --> AS
    SC --> SS
    SC --> ES
    DC --> DS
    DC --> ES
    SLC --> SLS
    SLC --> ES

    SS --> SHR
    DS --> DOCR
    DS --> STORABS
    SLS --> SLNKR
    ES --> EVTR
    AS --> UR
    AS --> OTR
    AS --> RTR

    SHR --> DBROUTER
    DOCR --> DBROUTER
    SLNKR --> DBROUTER
    EVTR --> DBROUTER
    UR --> DBROUTER
    OTR --> DBROUTER
    RTR --> DBROUTER

    M2 --> DBROUTER
    M3 --> ABACENG
```

---

## 3. Shipment State Machine

```mermaid
stateDiagram-v2
    [*] --> created : Transporter creates shipment

    created --> assigned : Transporter assigns trucker\n(trucker_id set)
    assigned --> pod_uploaded : Driver uploads POD\n(or trucker if no driver)
    pod_uploaded --> shared : Transporter generates share link\n(core fields locked)

    created --> created : Transporter edits metadata\nTransporter attaches docs\n(no trucker assigned)
    assigned --> assigned : Trucker assigns driver + vehicle\nTrucker reassigns driver (event logged)\nTrucker attaches docs (if no driver)\nTransporter reassigns trucker (event logged)
    pod_uploaded --> pod_uploaded : Transporter edits metadata only\nTransporter generates share link
    shared --> shared : Transporter edits metadata only\n(broker contact · notes)\nShare link revoked and regenerated
```

---

## 4. Document Attachment Permission Flow (Q5 Custom Rule)

```mermaid
flowchart TD
    REQ["Upload Document Request\nuser + shipment"] --> R1{user.role?}

    R1 -->|driver| D1{shipment.driver_id\n=== user.id?}
    D1 -->|yes| ALLOW["✅ ALLOW\nupload document"]
    D1 -->|no| DENY["❌ DENY 403"]

    R1 -->|trucker| T1{shipment.trucker_id\n=== user.id?}
    T1 -->|no| DENY
    T1 -->|yes| T2{shipment.driver_id\nis null?}
    T2 -->|yes| ALLOW
    T2 -->|no| DENY

    R1 -->|transporter| P1{user.org_id\n=== shipment.org_id?}
    P1 -->|no| DENY
    P1 -->|yes| P2{shipment.trucker_id\nis null?}
    P2 -->|no| DENY
    P2 -->|yes| P3{shipment.driver_id\nis null?}
    P3 -->|no| DENY
    P3 -->|yes| ALLOW
```

> **Mobile-only feature (no API change needed):**
> Transporter can share a document from WhatsApp to the app via the OS Share Sheet.
> The app registers as a share target on iOS / Android.
> The OS passes the file to the app → app shows a shipment picker → calls the document upload API.
> This is entirely a mobile app concern.

---

## 5. Tenant DB Resolution Flow

```mermaid
sequenceDiagram
    participant App
    participant API
    participant MasterDB
    participant TenantDB

    App->>API: Request with JWT (contains org_id)
    API->>API: ① verifyJWT → extract user_id + org_id
    API->>MasterDB: ② SELECT db_host, db_name WHERE id = org_id
    MasterDB-->>API: db_host: pg-org1.internal, db_name: pod_org1
    API->>TenantDB: ③ Acquire pooled connection to org DB
    API->>API: ④ Inject db client into request context
    API->>TenantDB: ⑤ Execute business query
    TenantDB-->>API: Result
    API-->>App: Response
```

> Connection pools are maintained per tenant. On MVP, use a simple pool map:
> `Map<org_id, PgPool>`. On scale, use PgBouncer per tenant or a connection proxy.

---

## 6. Document Upload Flow — MVP vs Future

### MVP (Local Storage)
```mermaid
sequenceDiagram
    participant App
    participant API
    participant LocalDisk

    App->>App: Client-side compress image\n(JPEG ~75%, max 2560px)\nReject if > 5 MB
    App->>API: POST /shipments/:id/documents\nmultipart/form-data {file, doc_type}
    API->>API: ① Auth + ABAC (doc permission rule)
    API->>API: ② Validate: size ≤ 5MB, MIME in allowlist
    API->>LocalDisk: ③ Save to /storage/{org_id}/{shipment_id}/{uuid}.jpg
    API->>TenantDB: ④ INSERT shipment_documents
    API->>TenantDB: ⑤ INSERT shipment_events (document_uploaded)
    API-->>App: 201 { document_id, doc_type, created_at }
```

### Future (S3 Presigned URL — same IStorageService interface)
```mermaid
sequenceDiagram
    participant App
    participant API
    participant S3

    App->>App: Client-side compress image
    App->>API: POST /shipments/:id/documents/upload-url\n{ doc_type, mime_type, file_size }
    API->>API: ① Auth + ABAC
    API->>S3: ② Generate presigned PUT URL (15 min TTL)
    API-->>App: { upload_url, document_id }
    App->>S3: ③ PUT file directly to S3 (no API bandwidth used)
    App->>API: ④ POST /shipments/:id/documents/confirm\n{ document_id }
    API->>TenantDB: ⑤ Mark document confirmed + INSERT event
    API-->>App: 200 { document confirmed }
```

---

## 7. Tech Stack Recommendation

| Layer | MVP Choice | Rationale |
|---|---|---|
| **API Framework** | Node.js + Fastify | Fast, schema validation built-in, TypeScript-friendly |
| **Language** | TypeScript | Type safety for ABAC policies + DB models |
| **ORM / Query Builder** | Prisma (per-tenant) | Schema-per-DB support, migration runner scriptable |
| **Master DB** | PostgreSQL | Same stack as tenant DBs |
| **Tenant DBs** | PostgreSQL | Reliable, JSON support for event payloads |
| **Auth OTP** | MSG91 / Twilio Verify | Indian market (MSG91), global fallback (Twilio) |
| **Storage (MVP)** | Local disk | Simple; abstracted behind IStorageService |
| **Storage (Future)** | Cloudflare R2 | S3-compatible, no egress fees, India CDN |
| **Mobile App** | React Native | One codebase, iOS + Android |
| **Hosting (MVP)** | Single VPS (DigitalOcean / Railway) | Low cost, easy deploy |
| **Hosting (Scale)** | Multiple VPS + Nginx LB | Horizontal scale path |

---

*Prepared by: Oz — Senior Solution Architect*
*Project: SC R&DT · POD Tracker · Feb 2026*
