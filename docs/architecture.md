# Architecture

## 1. Purpose

This document defines the technical architecture for a Québec-first, Canada-ready **Animal Welfare & Municipal Animal Services OS**.

The platform begins as a focused shelter-management application and must be capable of growing into a commercial multi-tenant SaaS without requiring a rewrite of the core domain.

The architecture optimizes for:

- A small development team
- Fast iteration
- Strong tenant isolation
- Québec-first data residency
- Privacy by design
- Native `fr-CA` and `en-CA`
- Reliable shelter and field workflows
- Replaceable external integrations
- Clear module boundaries
- Data ownership and portability
- A practical hobby-to-commercial growth path

The default rule is:

> Choose the simplest architecture that preserves tenant isolation, privacy, auditability, domain integrity, and future extensibility.

---

# 2. Architecture Principles

## 2.1 Modular Monolith First

The backend is a **modular monolith**.

Do not introduce microservices, Kubernetes, distributed messaging, or independently deployed backend services until a demonstrated operational or organizational requirement justifies them.

Modules are vertical business slices.

Each module owns its:

- Domain model
- Application logic
- API endpoints
- Persistence configuration
- Authorization policies
- Background jobs
- Integration adapters
- Tests

Architecture tests enforce module boundaries.

---

## 2.2 API-First

The backend owns:

- Business rules
- Validation
- Authorization
- Persistence
- Audit
- Workflows
- Integrations

The frontend communicates with the backend through a documented REST API described by OpenAPI.

The OpenAPI contract generates the TypeScript frontend client.

---

## 2.3 Multi-Tenant From Day One

Multi-tenancy is an implementation requirement, not only a product principle.

The initial model is:

```text
Tenant = Organization

Organization
├── Locations / Sites
├── Jurisdictions served
├── Staff memberships
├── External portal memberships
└── Tenant-owned domain data
```

Tenant isolation is enforced at multiple layers:

```text
Request
  ↓
TenantContext
  ↓
Authorization
  ↓
EF Core tenant query filters
  ↓
PostgreSQL Row-Level Security
  ↓
Tenant-scoped persistence
```

Every **tenant-owned** database record contains a `TenantId`.

Intentionally global reference/system data does not require a tenant discriminator.

---

## 2.4 Provider-Neutral Integrations

External vendors must never become core domain dependencies.

Integrations are accessed through provider abstractions such as:

```text
LicensingProvider
MicrochipProvider
PaymentProvider
EmailProvider
SmsProvider
AccountingProvider
VeterinaryProvider
IdentityProvider
```

The platform remains operational when an external provider is unavailable.

**EmiliPet is a connector, never a platform dependency.**

---

## 2.5 Québec-First Without Québec-Only Design

Québec requirements are implemented through:

- Localization
- Jurisdiction rule packs
- Configuration
- Provider implementations
- Document templates
- Privacy policies

Generic domain concepts must not contain scattered province- or municipality-specific conditionals.

---

## 2.6 Privacy and Audit by Design

Privacy, retention, consent, access control, incident management, and auditability are platform capabilities rather than features added later.

---

# 3. High-Level Architecture

```text
┌──────────────────────────────────────────────────────────┐
│                 React + TypeScript + Vite                │
│                         PWA                              │
│                                                          │
│ Staff App │ Public Routes/Widget │ External Portal UI    │
└────────────────────────┬─────────────────────────────────┘
                         │ HTTPS / REST
                         ▼
┌──────────────────────────────────────────────────────────┐
│                ASP.NET Core / .NET 10                    │
│                  Modular Monolith                        │
│                                                          │
│ Animals │ People │ Movements │ Medical │ Operations      │
│ Engagement │ Municipal │ Reporting │ Platform            │
└────────────────────────┬─────────────────────────────────┘
                         │
              ┌──────────┼─────────────┐
              ▼          ▼             ▼
         PostgreSQL   Object        Hangfire
                      Storage       PostgreSQL
                         │
                         ▼
                 Provider Interfaces
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
    Licensing        Microchips        Payments
    Email/SMS        Accounting        Other APIs
```

---

# 4. Frontend Architecture

## 4.1 V1 Frontend

V1 ships **one primary frontend**.

Technology:

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- TanStack Table
- React Hook Form
- Zod
- Tailwind CSS
- shadcn/ui / Radix
- Orval
- Vitest
- Playwright
- PWA support

Do not ship a separate Next.js application in V1.

The initial public experience consists of:

- Public adoption listings
- Public animal profiles where needed
- Public forms
- Embeddable adoption/listing widget
- Limited adopter/foster/volunteer portal routes

A separate Next.js public portal may be introduced when SEO, social previews, content management, public traffic, or performance requirements justify maintaining a second frontend runtime.

---

## 4.2 API Client

**Orval** is the standard OpenAPI TypeScript client generator.

Generated code lives in:

```text
packages/api-client
```

Orval generates typed TanStack Query integrations from the backend OpenAPI document.

Generated client code is not manually edited.

---

## 4.3 PWA Strategy

The application is installable as a PWA.

Initial capabilities include:

- Responsive layouts
- Camera access
- Photo capture/upload
- QR/barcode workflows
- Fast kennel workflows
- Signature capture
- Mobile animal lookup
- Task completion

Full offline synchronization is not required for V1.

Later offline support should target specific workflows:

- Animal-control cases
- Field notes
- Photos
- GPS information
- Kennel observations
- Task completion

Do not attempt to make the entire application offline-first.

---

# 5. Backend Architecture

## 5.1 Technology

Backend:

- C#
- .NET 10
- ASP.NET Core
- REST
- OpenAPI
- EF Core
- PostgreSQL
- Hangfire
- xUnit

Dapper or raw SQL may be used selectively for reporting/query workloads where EF Core is demonstrably unsuitable.

---

## 5.2 Vertical Modular Structure

Do not organize the entire application horizontally as:

```text
Api/
Application/
Domain/
Infrastructure/
```

Instead:

```text
backend/
├── Shelter.Host/
│   ├── Program.cs
│   ├── Middleware/
│   └── Composition/
│
├── Modules/
│   ├── Animals/
│   ├── People/
│   ├── Movements/
│   ├── Medical/
│   ├── Operations/
│   ├── Engagement/
│   ├── Municipal/
│   ├── Reporting/
│   └── Platform/
│
├── BuildingBlocks/
│   ├── Persistence/
│   ├── Tenancy/
│   ├── Authorization/
│   ├── Auditing/
│   ├── Jobs/
│   ├── Documents/
│   ├── Communications/
│   └── Integrations/
│
└── ArchitectureTests/
```

`Communications` (email/SMS/in-app sending, templates, delivery tracking) is a building block rather than a business module because every module sends messages.

A module may internally use:

```text
Animals/
├── Domain/
├── Features/
│   ├── CreateAnimal/
│   ├── UpdateAnimal/
│   ├── GetAnimal/
│   └── SearchAnimals/
├── Persistence/
├── Authorization/
└── Tests/
```

Feature folders may colocate endpoints, commands/queries, validators, and handlers when doing so improves locality.

---

## 5.3 Module Boundaries

Modules may not directly manipulate another module's internal persistence model.

Cross-module interaction occurs through:

- Explicit contracts
- Application/domain events
- Stable identifiers
- Read models where justified

Architecture tests enforce allowed dependencies.

---

# 6. Initial Business Modules

Start with approximately nine top-level modules.

## 6.1 Animals

Owns:

- Animal identity
- Species/breed/colour
- Identifiers
- Photos/media
- Relationships
- Current summary state
- Animal timeline
- Community-cat/TNR information
- External identifiers

The Animal entity persists independently of any intake.

---

## 6.2 People

Owns:

- Contacts
- Households
- Owners
- Adopters
- Foster contacts
- Volunteers
- Donors
- Finders
- Surrenderers
- Complainants
- Witnesses
- Communication preferences
- Preferred language
- Duplicate detection/merge

A person can hold multiple roles simultaneously.

---

## 6.3 Movements

Owns the animal lifecycle/movement ledger:

- Intake
- Internal relocation
- Foster placement movement
- Adoption outcome
- Transfer
- Reclaim
- Return
- Escape
- Release/return-to-field
- Death

Adoption and foster appear in the animal's movement history but are **not reduced to only a movement row**.

Rich concepts such as:

- `AdoptionCase`
- `Application`
- `FosterPlacement`
- Agreements
- Payments
- Follow-ups

remain separate records linked to movement events.

---

## 6.4 Medical

Owns:

- Medical records
- Examinations
- Diagnoses
- Vaccinations
- Medications
- Treatments
- Prescriptions
- Surgery
- Sterilization
- Tests/labs
- Medical protocols
- Weight/BCS
- Medical alerts
- Medical inventory
- Medication/vaccine stock
- Treatment queues

Vaccinations and medications are not separate top-level modules in V1.

---

## 6.5 Operations

Owns:

- Locations
- Kennels
- Population management
- Tasks
- Operational workflows
- Events
- Appointments
- Staff/resource scheduling

Animal-control cases, field services, and dispatch belong to **Municipal**, not Operations.

---

## 6.6 Engagement

Owns:

- Adoption applications/workflows
- Foster program workflows
- Volunteers
- Donors/fundraising
- Donations
- Donation receipt records/configuration
- Public forms
- Public adoption publishing
- Lost/found submissions
- Partner/transfer workflows

Transfer movement itself remains part of Movements.

Communications is a building block (see 5.2), not part of Engagement.

Engagement is the module most likely to grow too large. When it does, split it along existing seams (for example `Placements` for adoption/foster and `Community` for volunteers, donors, and lost/found) rather than letting it become a catch-all.

---

## 6.7 Municipal

Owns:

- Jurisdictions
- Municipal contracts
- Rule packs
- Licensing-provider configuration
- Licence records
- Bylaw-related configuration
- Municipal reporting configuration
- Animal-control cases (bite reports, dangerous-dog files, complaints, inspections)
- Field-service workflows
- Dispatch when introduced
- Native licensing when introduced

---

## 6.8 Reporting

Owns:

- Operational reports
- Financial reports
- Municipal reports
- Scheduled reports
- Export jobs
- Report builder/read models
- Analytics queries

---

## 6.9 Platform

Owns:

- Tenant provisioning
- Authentication integration
- Staff memberships
- External identities
- Permissions
- Privacy workflows
- Consent
- Retention policies
- Audit access
- Integrations
- Webhooks
- Imports/exports, including migration importers (Animal Shelter Manager first)
- Feature flags
- SaaS administration

---

# 7. Core Domain Model

These remain separate first-class concepts:

```text
Animal
Person
Organization
Location
Jurisdiction
Case
Movement
MedicalEvent
ExternalIdentifier
```

Important additional concepts include:

```text
Intake
Outcome
AdoptionCase
Application
FosterPlacement
Licence
Task
Workflow
Document
Payment
Donation
InventoryItem
Communication
ConsentRecord
PrivacyRequest
AuditEvent
```

An animal is **not** an intake record.

Example:

```text
Animal
├── Intake #1
│   ├── Stray hold
│   ├── Medical events
│   └── Reclaim movement
│
├── Intake #2
│   ├── Owner surrender
│   ├── Foster placement
│   ├── Medical events
│   └── Adoption movement
│
└── External identifiers
    ├── Microchip
    ├── Municipal licence
    └── External system IDs
```

Important state transitions should have both:

1. Efficient current state.
2. Immutable historical/timeline events.

## 7.1 Three Histories, Three Purposes

The platform keeps three kinds of history. Each has one job and they must not be used interchangeably.

| History | Purpose | Audience | Example |
|---|---|---|---|
| **Movement ledger** | Legal/operational custody of the animal: where it is and who is responsible | Staff, reports, MAPAQ register, municipal reporting | Intake, foster placement, adoption, transfer |
| **Timeline events** | Human-readable story of the animal or case, including non-movement events | Staff UI | "Vaccinated", "Photo added", "Behaviour note" |
| **AuditEvent** | Who changed what, when, for accountability | Admins, privacy/security review | "User X changed adopter address" |

Rules:

- A state change, its movement row (if any), and its timeline event are written **in the same database transaction**.
- Audit events are captured by the audit mechanism (section 17), not hand-written as timeline entries.
- Reports read the movement ledger, never the timeline.
- The timeline may be rebuilt from domain records; the movement ledger and audit log are never rebuilt.

---

# 8. Multi-Tenancy

## 8.1 Tenant Model

An **Organization** is the SaaS tenant.

An organization may contain:

- Multiple facilities
- Multiple shelter locations
- Foster-only operations
- Multiple municipal contracts
- Multiple jurisdictions
- Multiple staff teams

A regional operator can serve multiple municipalities without requiring a separate SaaS tenant for every municipality.

---

## 8.2 Database Isolation

V1 uses:

> **Shared PostgreSQL database + shared schemas + `TenantId`.**

Every tenant-owned row contains:

```text
TenantId
```

Isolation layers:

1. Request-level `TenantContext`
2. Server-side authorization
3. EF Core global tenant query filters
4. PostgreSQL Row-Level Security
5. Tenant-aware indexes and unique constraints

Example:

```text
UNIQUE (TenantId, ShelterAnimalNumber)
```

rather than:

```text
UNIQUE (ShelterAnimalNumber)
```

---

## 8.3 PostgreSQL Row-Level Security

RLS is a database-level backstop, not a substitute for application authorization.

Runtime database roles:

- Must not use `BYPASSRLS`
- Must not unintentionally bypass policies through table ownership
- Use `FORCE ROW LEVEL SECURITY` where appropriate

Migration/admin roles remain separate from runtime roles.

### Tenant propagation to the database

RLS policies read the current tenant from a transaction-scoped session setting:

```text
current_setting('app.tenant_id', true)
```

Rules:

- The tenant is set with `SET LOCAL` (transaction-scoped) by an EF Core connection/transaction interceptor at the start of every unit of work. Never use session-level `SET`: pooled connections would carry the previous request's tenant.
- Every tenant-scoped operation runs inside an explicit transaction so `SET LOCAL` applies.
- If no tenant is set, policies return **no rows** (fail closed), never all rows.
- Hangfire jobs, imports, and integration handlers use the same mechanism after restoring `TenantId` from the job payload.
- Cross-tenant platform operations (tenant provisioning, platform admin) use a separate database role and connection string, never the runtime role.

Mandatory test: execute requests for tenant A and tenant B sequentially over the **same pooled connection** and assert no data leaks between them.

### Tenant-aware indexing

All tenant-owned tables use composite indexes and unique constraints that **start with `TenantId`**. Without this, RLS predicates degrade query performance as data grows.

---

## 8.4 Location and Jurisdiction Scoping

Permissions may be scoped to:

- Tenant
- Location/site
- Jurisdiction
- Team
- Specific program/record where justified

Example:

```text
User
└── OrganizationMembership
    ├── Roles
    ├── AllowedLocations
    └── AllowedJurisdictions
```

---

## 8.5 Tenant-Scoped Infrastructure

Tenant isolation extends beyond PostgreSQL.

### Object Storage

```text
tenants/{tenantId}/animals/{animalId}/...
tenants/{tenantId}/documents/...
tenants/{tenantId}/exports/...
```

Buckets remain private.

### Background Jobs

Every tenant-specific job carries an explicit `TenantId`.

Jobs restore and validate tenant context before accessing data.

### Cache

V1 uses in-process caching.

Tenant-owned cache keys include `TenantId`.

The same requirement applies if Redis is introduced later.

---

# 9. Authentication and Authorization

## 9.1 V1 Authentication

Use:

- ASP.NET Core Identity
- Secure cookie authentication
- HttpOnly cookies
- Secure cookies
- Appropriate SameSite configuration
- CSRF protection
- MFA support

Do not store browser authentication tokens in local storage.

The Vite SPA and ASP.NET Core backend should be served using a same-origin or appropriate same-site deployment model.

---

## 9.2 Authorization

Authorization is permission-based.

Examples:

```text
animal.read
animal.write
animal.move

medical.read
medical.write
medical.administer

people.read
people.write

adoption.review
adoption.approve

foster.manage
volunteer.manage

finance.read
finance.refund
donation.receipt

municipal.case.read
municipal.case.write

licence.read
licence.manage

report.view
report.export

privacy.request.manage
audit.read
```

Roles are collections of permissions.

All authorization is enforced server-side.

---

## 9.3 Staff vs External Users

Staff and external participants are separate authorization classes.

External users include:

- Foster families
- Volunteers
- Adopters/applicants
- Veterinary partners
- Rescue/transfer partners
- Municipal partner users

A matching email address must never implicitly grant staff access.

Model:

```text
UserAccount
├── StaffMembership(s)
└── ExternalPortalMembership(s)
```

Separate portal authorization policies or authentication schemes may be used where they improve isolation.

---

## 9.4 Future Identity

OpenIddict is **not required for V1**.

Municipal/enterprise SSO can use the appropriate external OIDC/SAML integration.

Introduce an authorization server such as OpenIddict only when the platform itself needs to issue OAuth/OIDC credentials to third-party applications or another concrete requirement justifies it.

Machine integrations may initially use scoped API credentials/service accounts.

---

# 10. Jurisdictions and Rule Packs

Québec- and municipality-specific rules are represented through versioned configuration.

```text
JurisdictionRulePack
├── JurisdictionId
├── EffectiveFrom
├── EffectiveTo
├── Province
├── Municipality
├── Tax rules
├── Licence rules
├── Hold-period rules
├── Fee schedules
├── Receipt/document rules
├── MAPAQ-related configuration
├── Municipal bylaw configuration
└── Required reporting
```

Rule packs are:

- Versioned
- Effective-dated
- Auditable
- Configurable where appropriate

Historical transactions retain the rule/version that applied when the event occurred.

Avoid scattered generic-domain conditionals such as:

```csharp
if (province == "QC")
{
    // special behaviour
}
```

when the behaviour belongs in a jurisdiction policy.

---

# 11. Localization and Bilingual Data

Localization applies to stored business data, not only UI strings.

## 11.1 Supported Locales

Initial locales:

```text
fr-CA
en-CA
```

Each user has a UI locale.

Each contact has a preferred communication language.

---

## 11.2 Bilingual Reference Data

Example:

```text
Breed
├── NameFr
└── NameEn

IntakeReason
├── LabelFr
└── LabelEn

OutcomeReason
├── LabelFr
└── LabelEn
```

Tenant-configurable content also supports both languages:

- Forms
- Document templates
- Email templates
- SMS templates
- Public descriptions
- Workflow messages

---

## 11.3 Communication Language

PDFs, email, SMS, public forms, and automated communication use the recipient's preferred language where known.

Recipient language is independent from the staff user's interface language.

---

## 11.4 Canadian Formatting

Support:

- Canadian postal addresses
- Postal codes
- Canadian phone numbers
- CAD
- Québec tax configuration where applicable
- Canadian dates
- Appropriate French/English terminology

Store timestamps in UTC and render them using the organization's configured timezone.

---

# 12. Search

V1 search stays inside PostgreSQL.

Use:

- PostgreSQL full-text search
- `unaccent`
- French stemming
- English stemming where appropriate
- Trigram/fuzzy matching where useful
- Normalized identifier fields

Search must be accent-insensitive for common staff workflows.

For example:

```text
Éclair
eclair
```

should resolve to the same animal when appropriate.

Implementation notes:

- Create custom text-search configurations combining `unaccent` with the stemmer, e.g. `fr_unaccent` (French) and `en_unaccent` (English).
- Free-text fields store or derive a per-row language (from the record or the tenant default) and build the `tsvector` with the matching configuration. Where the language is unknown, index with both configurations.
- Names and identifiers (animal names, microchips, phone numbers, IDs) use normalized columns plus trigram matching rather than stemming.

Global search covers:

- Animal name
- Shelter ID
- Microchip
- Licence
- Person
- Phone
- Email
- Address
- Incident/case
- Kennel/location
- Adoption/application IDs

Do not introduce Elasticsearch/OpenSearch in V1.

---

# 13. Background Jobs

Use:

> **Hangfire + PostgreSQL storage**

Jobs include:

- Reminders
- Scheduled communications
- Report generation
- Export generation
- Retention/purge processing
- Integration imports/exports
- Reconciliation
- Document generation
- Retryable webhook processing

Redis is not part of V1.

Introduce Redis only when measured scaling requirements justify distributed caching or other Redis-specific capabilities.

---

# 14. Integrations

## 14.1 Provider Model

External systems are hidden behind platform-owned interfaces.

Provider capabilities are explicit because providers may support different operations.

Example:

```text
LicensingProviderCapabilities
├── Lookup
├── Import
├── Export
├── Issue
├── Renew
└── RealtimeSync
```

---

## 14.2 Licensing

Do not assume real-time EmiliPet API access.

V1 begins with generic file-based interoperability:

```text
LicensingProvider
└── CsvLicensingProvider
    ├── Import
    ├── Export
    ├── Validation
    └── Reconciliation
```

Future providers may include:

```text
LicensingProvider
├── EmiliProvider
├── MunicipalApiProvider
├── OtherVendorProvider
└── NativeLicensingProvider
```

An `EmiliProvider` is implemented only when suitable documented/partner API access is available.

Native licence issuing is not a V1 requirement.

---

## 14.3 Microchips

Microchip records remain provider-neutral.

Store:

- Chip number
- Registry/provider
- External registration ID
- Registration state
- Sync metadata

Provider APIs remain optional adapters.

---

## 14.4 Email and SMS

Communication records include:

- Tenant
- Recipient
- Channel
- Template/version
- Preferred language
- Delivery state
- Provider message ID
- Related record
- Consent/purpose information where applicable

---

## 14.5 Webhooks

Inbound webhooks:

- Verify provider signatures
- Persist provider event IDs
- Deduplicate events
- Process asynchronously where appropriate
- Be idempotent
- Record failures/retries

Outbound platform webhooks use:

- Signed payloads
- Retry policies
- Delivery logs
- Tenant-scoped secrets

---

# 15. Payments and Donations

Payment timing follows the product spec. Adoption workflows require fees, donations, and receipts, so the payment abstraction and first provider are expected early.

## 15.1 Provider

Preferred initial commercial implementation:

```text
PaymentProvider
└── StripeConnectProvider
```

Each tenant receives its own connected-account configuration.

The Stripe Connect account type (Standard vs Express) must be decided in an ADR before implementation. It determines tenant onboarding flow, who handles disputes/refund liability, dashboard access for shelters, and platform-fee mechanics.

---

## 15.2 Requirements

- Idempotent payment creation
- Deduplicated webhook processing
- No raw card storage
- Refund support
- Reconciliation
- Tenant-scoped provider IDs
- Immutable financial transaction history
- Clear separation between platform fees and shelter funds

---

## 15.3 Donations vs Fees

Donations are separate from operational fees.

```text
Payment
├── ServiceFee
├── AdoptionFee
├── ReclaimFee
├── LicenceFee
└── OtherCharge

Donation
├── Gift
├── Campaign
├── Donor
└── Receipt
```

Official donation receipt functionality is enabled only where the organization and transaction meet applicable requirements.

Receipt configuration belongs in organization/jurisdiction policies.

---

# 16. Privacy and Law 25 Design

Privacy requirements are part of the architecture.

This section defines product architecture and does not replace organization-specific privacy or legal review.

## 16.1 Québec Residency

Production workloads containing customer personal information are hosted in **Québec by default**.

This includes, where supported by the selected infrastructure:

- Application workloads
- PostgreSQL
- Object storage
- Backups
- Generated exports
- Search indexes
- Operational logs containing personal information

Do not treat generic "Canadian hosting" as equivalent to Québec residency.

The default deployment target is **AWS `ca-central-1` (Canada Central, Montréal area)**. See section 25.

Service-level residency must be verified before production deployment.

---

## 16.2 External Processors

Québec hosting does not guarantee every external processor keeps information in Québec.

Each integration documents:

- Data categories transmitted
- Purpose
- Provider
- Processing/storage location where known
- Retention
- Security controls
- Privacy requirements

This includes:

- Payments
- Email/SMS
- Error monitoring
- Support tooling
- Analytics
- Identity providers

Out-of-Québec processing must occur intentionally rather than accidentally through a SaaS dependency.

---

## 16.3 Privacy Assessments

The platform should provide documentation and controls that simplify customer privacy assessments.

Québec hosting reduces unnecessary out-of-Québec transfers but does **not** imply that all other privacy-assessment obligations disappear.

---

## 16.4 Privacy-Protective Defaults

Defaults include:

- Least-privilege permissions
- Private documents/media
- Minimal public profile fields
- Minimal personal information in logs
- Explicit retention policies
- Purpose-aware data collection
- Safe communication defaults

---

## 16.5 Consent

Maintain structured consent records where applicable.

```text
ConsentRecord
├── PersonId
├── Purpose
├── Channel
├── State
├── Source
├── Timestamp
├── DisclosureVersion
└── Withdrawal
```

Do not model every privacy requirement as consent.

---

## 16.6 Retention and Purge

Retention is policy-driven.

```text
RetentionPolicy
├── RecordType
├── Jurisdiction
├── RetentionPeriod
├── Trigger
├── Action
└── Exceptions / LegalHold
```

Scheduled jobs identify records eligible for:

- Destruction
- Permitted anonymization
- Archival
- Manual review

Purge operations are auditable.

---

## 16.7 Privacy Requests

Support workflows for:

- Access requests
- Rectification requests
- Record collection/export
- Identity verification
- Deadlines
- Review
- Fulfilment
- Audit history

---

## 16.8 Confidentiality Incidents

Maintain an incident register containing:

- Incident type
- Discovery time
- Information affected
- Tenant affected
- Assessment
- Actions
- Notifications
- Resolution
- Supporting documents

---

# 17. Audit Architecture

Audit is a first-class platform capability.

## 17.1 Audit Event

Use an append-only model:

```text
AuditEvent
├── Id
├── TenantId
├── ActorId
├── ActorType
├── EntityType
├── EntityId
├── Action
├── BeforeJson
├── AfterJson
├── TimestampUtc
├── CorrelationId
├── Source
└── Metadata
```

---

## 17.2 Personal Information in Audit Payloads

An append-only audit log that stores raw personal values would defeat retention purges and privacy deletion: the data would survive in `BeforeJson`/`AfterJson` after the source record is destroyed.

Decision: **crypto-shredding**.

- Fields are classified (personal vs non-personal) in entity configuration.
- Non-personal values are stored in plain form.
- Personal values in audit payloads are encrypted with a **per-person data key**.
- Data keys are stored encrypted by a master key held in managed key storage (KMS).
- When a person is purged or anonymized, their data key is destroyed. Audit rows remain intact (actor, action, field names, timestamps), but personal values become unrecoverable.

The same rule applies to timeline events and any other append-only store that may contain personal information.

## 17.3 EF Core Interception

A `SaveChangesInterceptor` captures ordinary tracked EF changes.

The interceptor is not the only audit mechanism.

Operations that bypass tracked changes—including raw SQL, bulk operations, integrations, administrative operations, and background jobs—must explicitly emit audit events when required.

---

## 17.4 Audit Guarantees

Audit records:

- Are append-only for normal application users
- Are tenant-scoped
- Cannot be edited through ordinary domain APIs
- Include actor/source
- Capture before/after values for sensitive changes
- Carry correlation IDs
- Have controlled retention
- Are exportable to authorized users

---

# 18. Documents and Object Storage

Use private S3-compatible object storage.

Local development may use MinIO.

Objects use tenant-scoped keys:

```text
tenants/{tenantId}/animals/{animalId}/...
tenants/{tenantId}/documents/...
tenants/{tenantId}/exports/...
```

Buckets remain private.

Document metadata stays in PostgreSQL.

```text
Document
├── TenantId
├── ObjectKey
├── FileName
├── ContentType
├── Size
├── Hash
├── Classification
├── CreatedBy
├── CreatedAt
└── RelatedEntity
```

Clients receive short-lived signed URLs only after server-side authorization.

## 18.1 Upload Processing

All uploads pass through a processing pipeline before becoming available:

- **Content validation:** verify actual file type, not only extension or declared content type; enforce size limits.
- **Metadata stripping:** remove EXIF/GPS metadata from images. Photos from finders and fosters often carry the location of a private home.
- **Image derivatives:** generate resized variants (thumbnail, listing, full) on upload; serve derivatives, not originals, on public surfaces.
- **Malware scanning:** required for files submitted through public forms or external portals before staff can open them.

Uploads remain quarantined (not downloadable) until processing completes.

---

# 18A. Public Surface

Public, unauthenticated access is isolated from the staff application.

## 18A.1 Public API

- Separate route group (e.g. `/public/{tenantSlug}/...`) with read-only endpoints.
- Anonymous; **no cookies** are accepted or issued.
- CORS allows cross-origin reads for the embeddable widget.
- Rate limiting per IP and per tenant.
- Returns only fields explicitly marked public (privacy-protective default); never staff notes, finder/owner details, or internal IDs beyond what the listing needs.

## 18A.2 Public Forms

- Spam protection on all public submissions (CAPTCHA or equivalent challenge).
- The CAPTCHA provider is an external processor and is documented per section 16.2 (data sent, processing location).
- Submissions enter a review queue; they never write directly into operational records.

## 18A.3 Social Sharing Previews

Rescues rely heavily on Facebook/Instagram sharing. A client-rendered SPA cannot provide per-animal preview tags to crawlers.

The backend exposes a lightweight share endpoint (e.g. `/s/{tenantSlug}/{animalPublicId}`) that:

- Returns minimal server-rendered HTML with Open Graph/Twitter meta tags (name, photo derivative, short description, localized).
- Redirects human visitors to the SPA or the shelter's own page embedding the widget.

This is required from the Pilot (see product-spec §41.2) and does not justify introducing Next.js.

---

# 19. Data Protection and Operations

## 19.1 Encryption

Require:

- TLS in transit
- Database encryption at rest
- Object-storage encryption at rest
- Encrypted backups
- Managed secret storage
- No committed production secrets

---

## 19.2 Backups

PostgreSQL must support point-in-time recovery.

Backup strategy includes:

- Automated backups
- PITR/WAL or managed equivalent
- Defined retention
- Encrypted backup storage
- Québec residency by default
- Periodic restore drills

A backup is not considered reliable until restore procedures have been tested.

---

## 19.3 Recovery

RPO and RTO targets are documented before commercial production.

Do not promise recovery objectives unsupported by the selected infrastructure.

---

## 19.4 Database Migrations

Migrations:

- Are version-controlled
- Run through deployment pipelines
- Are tested against production-like data
- Avoid destructive changes without migration/backfill plans
- Preserve tenant isolation

---

# 20. Community Cats / TNR

Community-cat and TNR workflows are explicitly supported.

```text
Animal
  ↓
Field Case / Capture
  ↓
Program Entry / Intake
  ↓
Medical
  ├── Sterilization
  └── Vaccination
  ↓
Community Cat Program
  ↓
Return-to-Field Movement
```

Support:

- Colony/location
- Caregiver
- Capture
- Treatment
- Sterilization
- Vaccination
- Identification/ear-tip
- Return location
- Follow-up
- Re-entry

This workflow validates the separation of Animal, Person, Case, Location, MedicalEvent, Program, and Movement.

---

# 21. Transfer Network

Transfers use the Movement model plus partner workflow records.

A transfer package may contain:

- Animal identity
- Photos
- Medical history
- Vaccinations
- Microchip information
- Behaviour information
- Documents
- Transfer agreement
- Chain of custody

The first implementation is export/import of transfer packages between organizations. Direct organization-to-organization and cross-platform transfers build on the same package format later.

Phasing is defined in the product spec.

---

# 22. Volunteers and Fundraising

These are explicit product capabilities even when deeper functionality is phased.

## 22.1 Volunteers

Volunteer records reuse People and add:

- Application
- Approval
- Skills
- Training
- Certifications
- Availability
- Shifts
- Hours
- Assignments
- Portal permissions

## 22.2 Donors and Fundraising

Donors reuse People.

Engagement owns:

- Donations
- Campaigns
- Recurring-gift metadata
- Tribute gifts
- Receipt records
- Communication preferences

Advanced fundraising CRM functionality may remain an external integration.

---

# 23. Public API and Webhooks

The application exposes REST/OpenAPI.

Example resources:

- Animals
- People
- Movements
- Medical events
- Cases
- Tasks
- Locations
- Applications
- Foster placements
- Licences
- Payments
- Donations

Example outbound events:

```text
animal.created
animal.updated
animal.adopted
animal.transferred
animal.reclaimed

medical.vaccine.administered
medical.treatment.completed

case.created

licence.updated

donation.received
```

External API access is tenant- and permission-scoped.

---

# 24. Observability

Keep V1 observability simple.

Start with:

- Structured ASP.NET Core logging
- Correlation/request IDs
- Health checks
- Hangfire monitoring
- Sentry where privacy configuration is acceptable

Do not send unrestricted personal information to telemetry providers.

Scrub sensitive fields from logs and telemetry.

OpenTelemetry may be added when there is a concrete requirement for vendor-neutral traces/metrics or more sophisticated production observability.

External telemetry processing outside Québec must be documented and reviewed.

---

# 25. Infrastructure and Hosting

## 25.1 Residency Requirement

Commercial production is deployed to a **Québec region by default**.

Do not select Toronto merely because it is a Canadian cloud region.

Cloud selection must verify:

- Physical region
- Managed-service availability
- Backup location
- Replication location
- Log location
- Disaster-recovery behaviour

### Default: AWS `ca-central-1`

The default production target is **AWS `ca-central-1` (Canada Central, Montréal area)**.

Rationale:

- Located in Québec.
- Multiple availability zones, so high availability does not require leaving the region.
- Managed PostgreSQL, object storage, and container hosting are available in-region.

Azure Canada East (Québec City) was considered but is not the default: its paired region is Canada Central (**Toronto**), so geo-redundant storage and backups replicate outside Québec unless explicitly disabled, and managed-service/availability-zone coverage in Canada East must be verified service by service.

The cloud provider remains an infrastructure choice rather than an application dependency.

---

## 25.2 Replication

Automatic geo-replication, backup replication, failover, and paired-region behaviour must be reviewed.

A Québec-residency requirement must not be accidentally defeated by automatic replication to another province.

For the AWS default:

- High availability uses **multi-AZ within `ca-central-1`**, not cross-region replicas.
- Automated backups and snapshots stay in `ca-central-1`; cross-region snapshot copy is disabled.
- Object-storage cross-region replication is disabled.
- Any future off-site/DR copy must target another Québec location or be explicitly reviewed as an out-of-Québec transfer.

---

## 25.3 Deployment

Use containers for application packaging.

Initial production should favor:

- Managed application/container hosting
- Managed PostgreSQL
- Private object storage
- Low operational overhead

Do not use Kubernetes initially.

---

## 25.4 Infrastructure as Code

Infrastructure must become reproducible before commercial production.

Use **OpenTofu** (Terraform-compatible) targeting `ca-central-1` only.

Production infrastructure should not exist solely as manual console configuration.

---

# 26. Repository Structure

Use a monorepo.

Workspace tooling:

- pnpm
- Turborepo
- .NET solution/projects
- GitHub Actions

Structure:

```text
shelter-platform/
├── apps/
│   └── admin/
│       ├── src/
│       └── tests/
│
├── packages/
│   ├── ui/
│   ├── api-client/
│   ├── config/
│   └── adoption-widget/
│
├── backend/
│   ├── Shelter.Host/
│   ├── Modules/
│   │   ├── Animals/
│   │   ├── People/
│   │   ├── Movements/
│   │   ├── Medical/
│   │   ├── Operations/
│   │   ├── Engagement/
│   │   ├── Municipal/
│   │   ├── Reporting/
│   │   └── Platform/
│   ├── BuildingBlocks/
│   └── ArchitectureTests/
│
├── infrastructure/
│   ├── docker/
│   └── deployment/
│
├── docs/
│   ├── architecture.md
│   ├── product-spec.md
│   └── adr/
│
├── pnpm-workspace.yaml
├── turbo.json
└── README.md
```

---

# 27. Testing Strategy

## Backend

Use xUnit for:

- Domain tests
- Feature tests
- Authorization tests
- Tenant-isolation tests
- Integration tests
- Persistence tests
- Architecture tests

Tenant-isolation tests are mandatory.

Test cross-tenant access through:

- Entity lookup
- Normal queries
- Search
- Reports
- Background jobs
- Documents
- Imports/exports

---

## Frontend

Use:

- Vitest
- Playwright

Critical E2E workflows include:

- Sign-in
- Animal intake
- Animal search
- Location movement
- Medical treatment
- Foster placement
- Adoption
- Lost/found
- Permission enforcement
- Bilingual workflows

---

# 28. Development Environment

Local infrastructure uses Docker Compose.

Expected dependencies:

```text
PostgreSQL
MinIO / S3-compatible storage
Mail testing service
ASP.NET Core application
Vite frontend
```

Redis is not required.

Seed data should contain:

- French and English records
- Multiple tenants
- Multiple locations
- Multiple jurisdictions
- Staff identities
- External identities
- Representative animal/medical/movement data

---

# 29. CI/CD

Use GitHub Actions for:

- Build
- Lint
- Unit tests
- Integration tests
- Architecture tests
- Frontend tests
- OpenAPI generation/validation
- Orval generation checks
- Container builds
- Database migration validation
- Deployment

Generated API clients must remain synchronized with the OpenAPI contract.

Production deployments require controlled migration and rollback procedures.

---

# 30. Product Phases

Feature phasing (V0/V1/V2/V3) is defined **only** in `product-spec.md`. This document does not repeat feature lists, to avoid the two drifting apart.

Architecture constrains phases only through infrastructure. Until a measured requirement justifies otherwise, every phase runs on:

```text
React/Vite PWA
ASP.NET Core modular monolith
PostgreSQL
Private object storage
Hangfire/PostgreSQL
```

No Redis.

No Kubernetes.

No separate search engine.

No dedicated authorization server.

No second frontend runtime.

---

# 31. Explicit V1 Non-Goals

Do not build in V1 without a concrete requirement:

- Microservices
- Kubernetes
- Redis
- Elasticsearch/OpenSearch
- Separate Next.js application
- Native mobile applications
- Full offline synchronization
- Native municipal licence issuance
- Generic OAuth authorization server
- Data warehouse
- Kafka/RabbitMQ
- Customer-specific code forks

---

# 32. Architecture Decision Summary

| Area | Decision |
|---|---|
| Backend | .NET 10 / ASP.NET Core |
| Architecture | Vertical modular monolith |
| Frontend | React + TypeScript + Vite |
| Public V1 | Public routes + embeddable widget |
| Future public portal | Next.js when justified |
| API | REST + OpenAPI |
| TypeScript client | Orval |
| ORM | EF Core |
| Reporting queries | Dapper/raw SQL selectively |
| Database | PostgreSQL |
| Tenancy | Shared DB + `TenantId` |
| Tenant safeguards | EF filters + PostgreSQL RLS |
| Search | PostgreSQL FTS + `unaccent` + stemming |
| Auth | ASP.NET Identity + secure cookies |
| Authorization | Permission RBAC + scopes |
| Jobs | Hangfire + PostgreSQL |
| Cache | In-memory initially |
| Redis | Deferred |
| Storage | Private S3-compatible storage |
| Document access | Short-lived signed URLs |
| Hosting | AWS `ca-central-1` (Québec), multi-AZ, no cross-region replication |
| IaC | OpenTofu |
| Tenant → DB propagation | `SET LOCAL app.tenant_id` per transaction via interceptor; fail closed |
| Localization | `fr-CA` + `en-CA`, including stored data |
| Licensing V1 | Provider abstraction + CSV import/export |
| Emili | Optional provider when supported access exists |
| Native licensing | Later phase |
| Payments | Provider abstraction; Stripe Connect when introduced |
| Audit | Append-only `AuditEvent` |
| Personal data in audit/timeline | Crypto-shredding with per-person keys |
| Communications | Building block, not a business module |
| Public surface | Anonymous read-only API + share endpoint for social previews |
| Payments provider | Stripe Connect; account type decided by ADR |
| Privacy | Privacy/Law 25 foundations |
| Workspace | pnpm + Turborepo |
| Backend tests | xUnit |
| Frontend tests | Vitest + Playwright |
| Observability | Structured logs + Sentry initially where appropriate |
| OpenTelemetry | Deferred |
| CI/CD | GitHub Actions |

Each decision in this table is recorded as an ADR in `docs/adr/` with context, alternatives considered, and consequences. New significant decisions get an ADR before implementation.

---

# 33. Guardrails

1. Tenant isolation is never optional.
2. Authorization is enforced server-side.
3. External vendors remain replaceable.
4. EmiliPet is a connector, not a dependency.
5. Québec-specific behaviour belongs in policies/rule packs rather than scattered conditionals.
6. French and English apply to stored business content, not only UI strings.
7. Sensitive changes are auditable.
8. Customers can export their data.
9. Privacy and retention are designed into workflows.
10. Infrastructure complexity requires a demonstrated need.
11. Configuration is preferred over customer-specific forks.
12. High-impact welfare, medical, legal, adoption, and enforcement decisions remain human-controlled.

---

# 34. Near-Term Implementation Order

```text
1. Repository / CI / local infrastructure

2. Platform foundations (together, before any business module)
   ├── TenantContext + TenantId conventions
   ├── EF query filters
   ├── PostgreSQL RLS + SET LOCAL interceptor
   ├── Audit interceptor + crypto-shredding key store
   ├── Bilingual reference-data pattern (NameFr/NameEn)
   └── Tenant-isolation tests (incl. pooled-connection test)

3. Identity
   ├── ASP.NET Identity
   ├── cookie auth
   ├── staff memberships
   └── permissions

4. People (incl. consent records)

5. Animals

6. Locations + Movements (+ timeline, same-transaction rule)

7. Medical

8. Documents + object storage + upload pipeline

9. Communications building block

10. Tasks + operational workflows

11. Adoption + foster workflows (+ payments/receipts per product spec)

12. Public API, widget, share endpoint

13. Lost & found

14. Reporting + exports

15. ASM importer + licensing CSV provider

16. External portal identities

17. Volunteer / fundraising / municipal expansion
```

Tenant isolation, auditability, bilingual data, and privacy foundations are implemented first because retrofitting them after the domain grows is substantially more expensive.

---

# 35. Final Architecture Position

The platform starts deliberately simple:

```text
React / Vite PWA
       │
       ▼
ASP.NET Core Modular Monolith
       │
       ├── PostgreSQL
       ├── Private Object Storage
       └── Hangfire / PostgreSQL
```

Its sophistication belongs primarily in:

- Domain modeling
- Tenant isolation
- Permissions
- Workflows
- Auditability
- Privacy
- Localization
- Provider boundaries

—not in distributed infrastructure.

This provides a realistic hobby-to-commercial path:

- Simple enough for a small team to build
- Strong enough for real shelter operations
- Québec-first without becoming Québec-only
- Multi-location and multi-municipality ready
- Compatible with EmiliPet or other providers without depending on them
- Designed for strong data portability
- Capable of adding payments, native licensing, public portals, partner networks, offline field workflows, and enterprise identity later without replacing the core architecture