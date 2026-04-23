# Azen — Software Requirements Specification (SRS)

**Version**: 2.0
**Date**: April 2026
**Status**: MVP Definition
**Reference**: `mvp-design.md` for technical design

---

## 1. Introduction

### 1.1 Purpose

This document defines the software requirements for the Azen MVP — a mobile-first logistics coordination platform for Indian mid-sized transporters. It serves as the contract between product and engineering for what the MVP must deliver.

### 1.2 Problem Statement

Indian mid-sized transporters manage logistics operations primarily through WhatsApp chats and phone calls. This leads to:

- Scattered shipment details across chat threads
- Loss of critical documents (Invoice, LR, POD)
- No centralized shipment tracking
- Confusion between multiple parties (consignor, transporter, fleet owner, driver, consignee)
- No audit trail for assignments, deliveries, or document handoffs
- WhatsApp accounts can be hacked, lost, or chats deleted — destroying business records

### 1.3 Product Vision

Replace fragmented WhatsApp-based logistics operations with a structured, trackable, document-driven workflow platform — while being **faster than sending a document via WhatsApp**.

### 1.4 MVP Success Criteria

- Transporters can create and manage shipments end-to-end within the app
- POD and other documents are uploaded, stored, and shareable via secure links
- The system works even when fleet owners or drivers refuse to use the app
- All shipment history and documents are preserved permanently (no data loss from deleted chats)

---

## 2. Stakeholders

### 2.1 Primary Users (registered in system)

| Role | Description | MVP Access |
|---|---|---|
| Transporter | Broker / middleman who coordinates shipments between consignors and fleet owners. Primary system user. | Full access — creates shipments, assigns actors, manages documents, generates share links |
| Fleet Owner | Trucking company that owns vehicles and employs drivers. Assigned by transporter. | Manages assigned shipments — assigns drivers, uploads documents |
| Driver | Delivery executor who physically transports goods. Assigned by fleet owner or transporter. | Minimal UI — views assigned shipments, uploads POD |

### 2.2 External Actors (no login required, MVP)

| Role | Description | MVP Access |
|---|---|---|
| Consignor (Shipper) | Person or company sending goods. Initiates shipment request to transporter (via WhatsApp). | View-only via share link — sees shipment details and selected documents |
| Consignee (Receiver) | Person or company receiving goods. | View-only via share link |

### 2.3 Real-World Workflow (As-Is)

1. **Consignor** (e.g. Rajesh) sends shipment details to **Transporter** (e.g. Anil) on WhatsApp: truck type, goods, weight, consignee, price
2. **Transporter** forwards load details to **Fleet Owners** (e.g. Aditya) via WhatsApp or calls
3. **Fleet Owner** evaluates profitability, accepts or rejects
4. **Fleet Owner** assigns a **Driver** (e.g. Shivesh), shares trip details on WhatsApp
5. **Driver** reaches pickup, collects documents (Invoice from consignor, LR from fleet owner), shares photos on WhatsApp
6. **Driver** delivers goods to **Consignee** (e.g. Pulkit), gets stamp/signature on invoice (POD)
7. **POD flows back**: Driver → Fleet Owner → Transporter → Consignor (all via WhatsApp)
8. **Payment flows**: Consignor → Transporter → Fleet Owner → Driver

---

## 3. Functional Requirements

### FR-1: Authentication

**FR-1.1** Users authenticate via OTP sent to their registered phone number.

**FR-1.2** After OTP verification, the system returns the list of organisations the user belongs to. If the user belongs to no organisation, they are prompted to create one.

**FR-1.3** The system issues JWT access tokens (15-minute expiry) and refresh tokens (30-day expiry).

**FR-1.4** Refresh tokens are rotated on each use — the old token is revoked and a new pair is issued.

**FR-1.5** Users can log out, which revokes their active refresh token.

---

### FR-2: Organisation Management

**FR-2.1** A new user (with no org memberships) can create an organisation by providing a name and unique slug.

**FR-2.2** The user who creates an organisation is automatically assigned the `transporter` role.

**FR-2.3** Transporters can invite members to their organisation by providing a phone number, name, and role (`fleet_owner` or `driver`).

**FR-2.4** If the invited phone number does not exist in the system, a user account is created automatically.

**FR-2.5** Transporters can view all members and deactivate members.

**FR-2.6** A user can belong to multiple organisations (e.g. a fleet owner for one transporter and a driver for another).

---

### FR-3: Shipment Management

**FR-3.1** Only transporters can create shipments.

**FR-3.2** Shipment creation requires no mandatory fields other than the shipment itself. Reference number is auto-generated if not provided (format: `SHP-{YYYY}-{seq:05d}`). All other fields (consignor, consignee, goods description, notes) are optional.

**FR-3.3** Reference numbers must be unique within an organisation.

**FR-3.4** Shipment listing is role-filtered:
- Transporter: sees all shipments in their org
- Fleet Owner (member): sees only shipments assigned to them
- Fleet Owner (manager): sees all shipments in their org
- Driver: sees only shipments assigned to them

**FR-3.5** Transporters can edit all shipment fields at any time.

**FR-3.6** Fleet owners can edit vehicle number and notes on their assigned shipments.

---

### FR-4: Shipment Lifecycle

**FR-4.1** Shipments progress through four statuses: `created` → `assigned` → `pod_uploaded` → `shared`.

**FR-4.2** Transitions are forward-only. No backward transitions are allowed.

**FR-4.3** The `assigned` status can be skipped — a transporter can upload a POD directly on a `created` shipment, advancing it to `pod_uploaded`.

**FR-4.4** Status is auto-advanced by triggering actions:
- Assigning a fleet owner → `assigned` (if currently `created`)
- Uploading a POD document → `pod_uploaded` (if currently `created` or `assigned`)
- Generating a share link → `shared` (if currently `pod_uploaded`)

**FR-4.5** Transporters can also manually advance status without the triggering action.

---

### FR-5: Fleet Owner & Driver Assignment

**FR-5.1** Transporters can assign a fleet owner to a shipment. The fleet owner can be:
- **In-system**: selected from org members (linked by member ID)
- **External**: recorded as name + phone metadata, marked as "not in system"

**FR-5.2** Fleet owners (or transporters) can assign a driver to a shipment. The driver can be:
- **In-system**: selected from org members
- **External**: recorded as name + phone metadata

**FR-5.3** Vehicle number is provided during driver assignment.

**FR-5.4** Reassignment is allowed. When a fleet owner or driver is reassigned, the previous assignment is logged in the audit trail with the old and new values.

**FR-5.5** Phone numbers of external fleet owners and drivers are stored on the shipment to enable phone call coordination through the app.

---

### FR-6: Flexible Participation Model

This is the core differentiating requirement.

**FR-6.1** The transporter must never be blocked from managing a shipment, regardless of whether other actors are in the system.

**FR-6.2** When a fleet owner or driver is NOT in the system:
- The transporter records them as external (name + phone only)
- The transporter uploads documents on their behalf (received via WhatsApp or other channels)
- The transporter advances shipment status manually

**FR-6.3** When a fleet owner or driver IS in the system but is unavailable (logged out, app issues):
- The transporter has full override to manage the shipment on their behalf

**FR-6.4** The transporter always has permission to:
- Create and edit any shipment in their org
- Change shipment status (any valid forward transition)
- Upload and delete documents on any shipment
- Assign and reassign fleet owners and drivers
- Generate and revoke share links

**FR-6.5** When all actors are in the system and available, the standard permission cascade applies:
- Driver uploads POD and other delivery documents
- Fleet owner manages shipments assigned to them, assigns drivers
- Transporter oversees everything with full override

---

### FR-7: Document Management

**FR-7.1** The system supports the following document types: POD, Invoice, LR (Lorry Receipt), Weighbridge, E-Way Bill, Consignment Note, Custom.

**FR-7.2** Supported file formats: JPEG, PNG, WebP, PDF. Maximum file size: 5MB.

**FR-7.3** Document upload permissions:
- Transporter: can upload to any shipment in their org (always)
- Fleet Owner: can upload to shipments assigned to them
- Driver: can upload to shipments assigned to them

**FR-7.4** Only transporters can delete (soft-delete) documents.

**FR-7.5** Each document stores: file name, type, size, MIME type, who uploaded it, their role, and timestamp.

**FR-7.6** Documents are served via time-limited signed URLs (1 hour for authenticated users, 30 minutes for public share link access).

---

### FR-8: Share Links

**FR-8.1** Transporters can generate a shareable link for a shipment when its status is `pod_uploaded` or `shared`.

**FR-8.2** When generating a link, the transporter selects which document types to expose (e.g. only POD, or POD + LR).

**FR-8.3** Share links are Base62 tokens (10 characters), unique, and expire after 30 days by default (configurable).

**FR-8.4** External parties (consignor, consignee, or anyone with the link) can view the shipment details and selected documents without logging in.

**FR-8.5** Transporters can revoke share links at any time.

**FR-8.6** The system tracks access count and last access time for each share link.

**FR-8.7** Expired or revoked links return a clear error message.

---

### FR-9: Audit Trail

**FR-9.1** Every significant action on a shipment is logged as an immutable, append-only event.

**FR-9.2** Tracked event types:
- `shipment_created`, `status_changed`
- `fleet_owner_assigned`, `fleet_owner_reassigned`
- `driver_assigned`, `driver_reassigned`
- `vehicle_updated`
- `document_uploaded`, `document_deleted`
- `share_link_generated`, `share_link_revoked`
- `metadata_updated`

**FR-9.3** Each event records: who performed it (actor ID + role), when, and a JSON payload with relevant details (e.g. old/new values for reassignment).

**FR-9.4** Events are never updated or deleted.

---

## 4. Non-Functional Requirements

### NFR-1: Performance

**NFR-1.1** API response time: < 500ms for standard requests under normal load.

**NFR-1.2** Document upload: complete within 3 seconds for files up to 5MB.

**NFR-1.3** Share link resolution (public endpoint): < 300ms.

### NFR-2: Capacity (MVP)

**NFR-2.1** Support up to 10,000 shipments per organisation.

**NFR-2.2** Support up to 100 concurrent users.

**NFR-2.3** Support up to 50 documents per shipment.

### NFR-3: Security

**NFR-3.1** All API communication over HTTPS.

**NFR-3.2** OTPs are bcrypt-hashed before storage. Never stored or logged in plain text.

**NFR-3.3** Refresh tokens are SHA-256 hashed before storage.

**NFR-3.4** Document URLs are time-limited and cryptographically signed (HMAC-SHA256).

**NFR-3.5** Role-based access control (ABAC) enforced on every authenticated endpoint.

**NFR-3.6** OTP rate limiting: max 3 requests per phone per 60 seconds, max 5 verification attempts per OTP.

### NFR-4: Reliability

**NFR-4.1** All document uploads must be stored durably — no data loss on application restart (MVP: local disk with persistent volume).

**NFR-4.2** Database operations that modify state must be transactional.

### NFR-5: Maintainability

**NFR-5.1** Clean Architecture: API → Application → Domain → Infrastructure.

**NFR-5.2** Storage abstraction (`IStorageService`) must allow swapping local storage for Azure Blob Storage without API changes.

**NFR-5.3** ABAC policies implemented as pure functions, testable in isolation.

---

## 5. User Stories

### 5.1 Transporter Stories

**US-T1**: As a transporter, I want to create a shipment with consignor and consignee details so that I have a structured record of the job.

**US-T2**: As a transporter, I want to assign a fleet owner to a shipment so that they can manage the driver and vehicle assignment.

**US-T3**: As a transporter, I want to assign an external fleet owner (name + phone only) when they refuse to use the app, so that I am not blocked.

**US-T4**: As a transporter, I want to upload documents (POD, Invoice, LR) on behalf of an external fleet owner or driver, so that all documents are captured in the system regardless of who provides them.

**US-T5**: As a transporter, I want to advance a shipment's status manually when I am managing it myself, so that the shipment lifecycle reflects reality.

**US-T6**: As a transporter, I want to generate a share link and choose which document types are visible, so that I can share only relevant documents with the consignor or consignee.

**US-T7**: As a transporter, I want to see the complete audit trail of a shipment, so that I know who did what and when.

**US-T8**: As a transporter, I want to invite fleet owners and drivers to my organisation, so that they can access their assigned shipments in the app.

**US-T9**: As a transporter, I want to call a fleet owner or driver directly from the shipment screen (using their stored phone number), so that I can coordinate quickly.

### 5.2 Fleet Owner Stories

**US-F1**: As a fleet owner, I want to see all shipments assigned to me, so that I know my active jobs.

**US-F2**: As a fleet owner, I want to assign a driver and vehicle number to a shipment, so that the transporter knows who is handling the delivery.

**US-F3**: As a fleet owner, I want to upload documents on a shipment when no driver has been assigned yet, so that I can forward documents received from the driver via WhatsApp.

### 5.3 Driver Stories

**US-D1**: As a driver, I want to see my assigned shipments with pickup and delivery details, so that I know where to go.

**US-D2**: As a driver, I want to upload POD photos directly from my phone camera, so that delivery proof is captured immediately.

### 5.4 External Actor Stories

**US-E1**: As a consignor, I want to open a share link and view the POD document without logging in, so that I can verify delivery and process payment.

**US-E2**: As a consignee, I want to view shipment details via a share link, so that I know when to expect delivery.

---

## 6. Acceptance Criteria

### AC-1: Ideal Flow (all actors in system)

1. Transporter creates shipment → status = `created`
2. Transporter assigns fleet owner (in-system) → status = `assigned`
3. Fleet owner assigns driver + vehicle → driver details recorded, event logged
4. Driver uploads POD → status = `pod_uploaded`
5. Transporter generates share link (selects POD + LR visibility) → status = `shared`
6. Consignor opens link → sees shipment details and only POD + LR documents

### AC-2: External Actor Flow (fleet owner not in system)

1. Transporter creates shipment → status = `created`
2. Transporter assigns external fleet owner (name: "Aditya Transport", phone: "+91...") → status = `assigned`, `fleet_owner_in_system = false`
3. Transporter uploads Invoice and LR (received from fleet owner via WhatsApp) → documents stored
4. Transporter uploads POD (received from driver via WhatsApp) → status = `pod_uploaded`
5. Transporter generates share link → status = `shared`
6. Consignor opens link → sees documents

### AC-3: Transporter Self-Managed Flow (no assignment)

1. Transporter creates shipment → status = `created`
2. Transporter uploads POD directly (no fleet owner or driver assigned) → status = `pod_uploaded`
3. Transporter generates share link → status = `shared`

### AC-4: Auth Flow

1. New user enters phone → receives OTP
2. Verifies OTP → receives auth_code + empty org list
3. Creates organisation → receives JWT, logged in as transporter
4. Invites fleet owner by phone → fleet owner can now login via OTP and see the org

### AC-5: Share Link Expiry

1. Share link is generated with 30-day expiry
2. External party accesses link within 30 days → sees shipment + docs
3. External party accesses link after 30 days → sees "Link expired" error
4. Transporter revokes link → external party sees "Link revoked" error

---

## 7. Out of Scope (Deferred)

| Feature | Target Phase |
|---|---|
| Payment tracking | Phase 2 |
| Push notifications / SMS on status changes | Phase 2 |
| Pagination / sorting / search on list endpoints | Post-MVP |
| AI-assisted shipment creation (paste WhatsApp text) | Phase 1.5 |
| WhatsApp bot integration | Phase 2 |
| Consignor / Consignee as registered users | Phase 3 |
| Analytics dashboard | Phase 3 |
| Load circulation / marketplace | Phase 3 |
| GPS tracking | Phase 3 |
| In-app chat (replace WhatsApp) | Phase 4 |

---

## 8. Glossary

| Term | Definition |
|---|---|
| POD | Proof of Delivery — stamped/signed invoice confirming goods were delivered |
| LR | Lorry Receipt — transport document issued by the fleet owner |
| E-Way Bill | Electronic waybill required for inter-state goods movement in India |
| Consignor | The party sending goods (shipper) |
| Consignee | The party receiving goods |
| Transporter | Broker / middleman coordinating between consignor and fleet owner |
| Fleet Owner | Trucking company that owns vehicles and employs drivers |
| ABAC | Attribute-Based Access Control — authorization based on user/resource attributes |
| Share Link | Time-limited, token-based URL for external parties to view shipment documents |
