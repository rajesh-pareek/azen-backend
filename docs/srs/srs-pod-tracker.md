# 📄 Software Requirements Specification (SRS)

## Product: Logistics Shipment Tracking Platform (MVP)

Version: 1.0
Status: MVP Definition
Architecture: Mobile-first SaaS platform

---

# 1. Product Overview

The platform is a **mobile-first logistics coordination system** designed for transporters, truckers, and drivers to manage shipment assignments and Proof-of-Delivery (POD) documents.

The system replaces the current industry workflow where shipment details and delivery documents are exchanged through **WhatsApp chats and phone calls** , resulting in poor traceability and lost documentation.

The platform provides a **structured shipment lifecycle** , centralized document storage, and secure shareable links for brokers and shippers.

---

# 2. Goals of MVP

The MVP aims to:

• Provide structured shipment creation and assignment
• Enable drivers to upload POD images directly from the mobile app
• Allow transporters to share POD documents via secure links
• Reduce reliance on unstructured WhatsApp communication
• Maintain simple workflows faster than existing manual processes

The system must be **faster than sending a document via WhatsApp manually.**

---

# 3. User Roles

The system supports three internal roles and one external viewer.

## 3.1 Transporter

Primary owner of shipments.

Capabilities:

• Create shipment
• Assign trucker
• View shipment progress
• View driver information
• Share POD links with brokers/shippers
• Attach broker contact information

---

## 3.2 Trucker

Fleet operator responsible for assigning drivers.

Capabilities:

• View shipments assigned by transporter
• Assign driver
• Add vehicle number
• View POD uploads

---

## 3.3 Driver

Delivery executor.

Capabilities:

• View assigned shipments
• Upload Proof of Delivery images

Drivers have the **simplest UI** .

---

## 3.4 Broker / Shipper (External)

External parties access the shipment **through a shareable link** .

Capabilities:

• View shipment details
• View POD document
• Download POD

No login required.

---

# 4. Shipment Lifecycle

Shipment status progression is strictly forward.

States:

```
Created
→ Assigned
→ Driver Assigned
→ POD Uploaded
→ Shared
```

No backward transitions are allowed.

---

# 5. Functional Requirements

## 5.1 Authentication

Users authenticate via **OTP-based login** using their phone number.

Flow:

1. Enter phone number
2. Receive OTP
3. Verify OTP
4. System loads role-specific dashboard

Session authentication uses **JWT tokens** .

---

# 5.2 Transporter Workflow

### Create Shipment

Fields:

• Shipment Number (manual entry)
• Shipper Name (optional)

System sets:

```
Status = Created
```

---

### Assign Trucker

Transporter selects a trucker from a list.

System updates:

```
Status = Assigned
```

---

### Shipment Details View

Displays:

• Shipment Number
• Current Status
• Assigned Trucker
• Assigned Driver
• Vehicle Number
• Uploaded Documents

---

### Share Shipment Link

Once POD is uploaded:

Transporter can generate a shareable link.

System:

• Generates token
• Stores link in database
• Allows broker to view POD

Link expiration:

```
30 days
```

---

# 5.3 Trucker Workflow

Trucker dashboard shows shipments assigned by transporters.

Each shipment allows:

• Driver selection
• Vehicle number entry

After assignment:

```
Status = Driver Assigned
```

---

# 5.4 Driver Workflow

Driver dashboard lists assigned shipments.

Driver can:

• Open shipment
• Upload POD image using camera

After upload:

```
Status = POD Uploaded
```

---

# 5.5 Document Management

Documents are stored in **cloud storage** .

Supported types:

```
Proof of Delivery (POD)
Invoice (future)
Consignment Note (future)
```

MVP includes:

```
POD only
```

Documents store:

• file name
• storage location
• uploader role
• timestamp

---

# 5.6 WhatsApp Integration (MVP Feature)

The application supports **native OS sharing integration** .

### Contact Sharing

Transporter can share a broker phone number from WhatsApp into the app.

The contact will be stored in the shipment record.

Important constraint:

Broker contact is **visible only to the transporter** .

It will not be visible to:

• drivers
• truckers
• brokers

---

### Document Sharing

Users can share documents from WhatsApp into the app.

Process:

1. User opens document in WhatsApp
2. Selects “Share”
3. Chooses the app
4. Selects shipment
5. Document attaches to shipment

---

### WhatsApp Deep Link

Shipment cards display a **WhatsApp icon** when a broker contact exists.

Tapping it opens:

```
whatsapp://send?phone=<broker_phone>
```

This allows direct conversation with the broker.

---

# 6. Phase-Based Feature Expansion

## Phase 1 (MVP)

Includes:

• Manual shipment creation
• Driver assignment
• POD upload
• Shareable document link
• WhatsApp contact attachment
• WhatsApp document sharing

---

## Phase 1.5

AI-assisted shipment creation.

User pastes unstructured shipment text.

AI extracts:

• shipment number
• shipper name
• vehicle number

---

## Phase 2

WhatsApp forwarding bot.

Users forward shipment chat messages to a WhatsApp bot.

The system converts messages into structured shipments.

---

# 7. System Architecture

Architecture style:

```
Mobile App
↓
REST API (.NET Web API)
↓
Application Database (MySQL)
↓
Cloud Storage
```

---

# 8. Backend Technology

Backend stack:

• **.NET Web API**
• **Entity Framework Core**
• **MySQL database**
• **Azure cloud hosting**

Authentication:

• JWT tokens/OTP based JWT

---

# 9. Database Architecture

The system uses **logical multi-tenancy** .

All organisations share a single database.

Data isolation is enforced using:

```
organisation_id
```

---

# 10. Database Schema

## organisations

```
id
name
slug
plan
is_active
created_at
```

---

## users

```
id
phone
name
email
password_hash
is_active
created_at
```

---

## organisation_members

```
id
organisation_id
user_id
role
sub_role
joined_at
```

Roles:

```
transporter
trucker
driver
```

---

## shipments

```
id
organisation_id
shipment_number
shipper_name
status
trucker_id
driver_id
vehicle_number
broker_contact_name
broker_contact_phone
notes
created_by
created_at
updated_at
```

---

## shipment_documents

```
id
shipment_id
doc_type
storage_key
original_filename
file_size_bytes
mime_type
uploaded_by
uploader_role
created_at
```

---

## share_links

```
id
shipment_id
token
created_by
expires_at
access_count
created_at
```

---

## shipment_events

Tracks audit history.

```
id
shipment_id
event_type
actor_id
actor_role
payload
created_at
```

Examples:

```
shipment_created
trucker_assigned
driver_assigned
pod_uploaded
link_shared
```

---

# 11. Security Requirements

Security measures include:

• OTP authentication
• JWT session tokens
• Access control by organisation
• Role-based API authorization
• Secure document URLs
• Expiring share links

---

# 12. Performance Requirements

The system must support:

• 10,000 shipments per organisation
• 100 concurrent users
• document uploads under 3 seconds

---

# 13. UI/UX Principles

The mobile interface must be:

• simple
• fast
• large tap targets
• minimal navigation

Navigation model:

```
Stack navigation only
```

No:

• hamburger menus
• bottom tabs
• complex filters

---

# 14. Deployment

Backend hosted on:

```
Microsoft Azure
```

Components:

• Azure App Service (.NET API)
• Azure Storage (documents)
• MySQL database

---

# 15. Future Scalability

If the platform grows significantly:

Options include:

• database sharding by organisation
• dedicated database for enterprise customers

---

# 16. Success Metrics

MVP success will be measured by:

• shipments created per day
• POD uploads
• share link usage
• reduction in WhatsApp-only document exchanges

---

# 17. MVP Scope Summary

Included:

✔ Shipment creation
✔ Driver assignment
✔ POD upload
✔ Shareable POD links
✔ WhatsApp contact linking
✔ WhatsApp document sharing

Excluded (future):

❌ AI shipment parsing
❌ WhatsApp bot automation
❌ analytics dashboards
❌ multi-document workflows
