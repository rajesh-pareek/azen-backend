# Azen Backend API

Azen is a mobile-first logistics coordination platform designed to replace unstructured WhatsApp-based shipment workflows with a structured and traceable system.

This repository contains the backend API responsible for shipment lifecycle management, Proof-of-Delivery (POD) handling, and secure document sharing.

---

## Problem

Current logistics workflows rely heavily on:

- WhatsApp chats
- Phone calls
- Manual document sharing

This leads to:

- Poor traceability
- Lost POD documents
- No structured shipment tracking

Azen introduces a system that is faster and more reliable than existing manual workflows.

---

## MVP Scope

- Shipment creation and assignment
- Driver-based POD upload
- Shareable POD links for external stakeholders
- Minimal and fast workflows
- Reduced reliance on WhatsApp

---

## Architecture Overview

```
Mobile App
   ↓
REST API (.NET Web API)
   ↓
Auth DB (Users, OTP)
   +
App DB (Shipments, Documents)
   ↓
Cloud Storage (POD files)
```

---

## Authentication

- Phone number based login
- OTP verification
- JWT-based session handling

Authentication is isolated from business data using a dedicated Auth database.

---

## Database Design

### Auth Database

Responsible for identity and authentication:

- Users
- OTP logs

### App Database

Responsible for business data:

- Organisations
- Organisation members
- Shipments
- Shipment documents (POD)
- Share links
- Shipment events

---

## Shipment Lifecycle

```
Created
→ Assigned
→ Driver Assigned
→ POD Uploaded
→ Shared
```

State transitions are strictly forward-only.

---

## User Roles

### Transporter

- Creates shipments
- Assigns truckers
- Shares POD links

### Trucker

- Assigns drivers
- Adds vehicle details

### Driver

- Views assigned shipments
- Uploads POD

### Broker / Shipper

- Access via secure link
- View and download POD
- No authentication required

---

## Project Structure

```
src/
  Azen.Api            Entry point (controllers, configuration)
  Azen.Application    Business logic (use cases, interfaces)
  Azen.Domain         Core domain models (entities, enums)
  Azen.Infrastructure Persistence and external integrations

docs/
  architecture/       System design documents
  srs/                Product requirements
  links/              External resources
```

---

## Tech Stack

- .NET 8 Web API
- Entity Framework Core
- MySQL
- JWT authentication
- OTP-based login
- Azure (planned)
- Cloud storage for documents

---

## Configuration

Configuration is managed via:

```
appsettings.json
appsettings.Development.json
```

Key sections:

- ConnectionStrings (AuthDb, AppDb)
- Jwt
- Otp
- Storage
- ShareLinks

---

## Running the Project

Clone the repository:

```bash
git clone <repo-url>
cd azen-backend
```

Run the API:

```bash
dotnet run --project src/Azen.Api
```

Test endpoints:

- `/api/v1/health`
- `/swagger`

---

## Roadmap

Phase 1.5:

- AI-assisted shipment creation

Phase 2:

- WhatsApp bot integration

Future:

- Analytics
- Multi-document workflows
- Scalable database strategies

---

## Engineering Team

- Rajesh Pareek (rajeshpareekdevo@gmail.com) — Backend
- Aditya (adityasharma7737pw@gmail.com) — Backend
- Shivesh (shiveshtrivedi159@gmail.com) — Backend

---

## Engineering Principles

- Clean architecture
- Separation of concerns
- API-first design
- Simplicity over premature optimization
- Designed for scalability

---

## Notes

Azen is built to bring structure and traceability to logistics workflows, replacing fragmented communication with a consistent system.
