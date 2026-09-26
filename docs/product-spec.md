# Product Specification

## Québec-First Shelter & Rescue Management Platform

**Status:** Product direction  
**Initial market:** Québec shelters, independent SPAs/SPCAs, foster-based rescues, adoption organizations, and community-cat/TNR organizations  
**Long-term direction:** Canadian animal-welfare and municipal animal-services platform

---

# 1. Product Vision

Build modern shelter and rescue management software designed around the actual operational, regulatory, privacy, and bilingual needs of animal-welfare organizations in Québec.

The initial product is **not** an attempt to replace every shelter, municipal, licensing, dispatch, veterinary, fundraising, and animal-control system at once.

The initial goal is narrower:

> Help Québec shelters and rescues replace legacy shelter-management software with a modern, bilingual, privacy-conscious system that handles daily animal operations exceptionally well.

The product should be capable of growing into a broader animal-welfare platform without requiring a rewrite of its core domain model.

Long term, the platform may expand into:

- Municipal animal-service contracts
- Animal-control field operations
- Licensing integrations
- Native licensing
- Dispatch
- Veterinary collaboration
- Organization-to-organization transfer networks
- Public APIs
- Integration marketplace
- Advanced analytics
- Carefully scoped AI assistance

These are expansion paths, not V0 requirements.

---

# 2. Initial Target Market

The initial market is:

1. Independent animal shelters
2. SPAs/SPCAs
3. Foster-based rescues
4. Adoption organizations
5. Community-cat/TNR organizations
6. Shelter organizations that also perform limited municipal animal-service work

Direct municipal animal-services software is a future market rather than the primary V0/V1 positioning.

The product should nevertheless avoid architectural decisions that would prevent future municipal expansion.

---

# 3. Product Positioning

## Near-Term Positioning

> **Modern shelter and rescue management software built for Québec.**

The product combines:

- Animal records
- Shelter operations
- Medical records
- Foster management
- Adoption
- Payments and receipts
- Donations
- Community-cat/TNR workflows
- Québec compliance support
- Bilingual operation
- Privacy and auditability
- Migration from legacy shelter systems

into one operational system.

## Long-Term Positioning

The long-term product can evolve toward:

> **A Canadian animal-welfare operations platform connecting shelters, rescues, municipalities, licensing providers, veterinary partners, and animal-welfare organizations.**

The long-term vision must not expand the initial implementation scope prematurely.

---

# 4. Five Product Promises

The product should be organized around five promises rather than a long list of equal differentiators.

## 4.1 Run the Shelter

Provide one reliable operational system for:

- Animals
- People
- Intake
- Outcomes
- Locations
- Medical care
- Foster
- Adoption
- Documents
- Payments
- Tasks
- Daily shelter operations

## 4.2 Operate Correctly in Québec

Provide a dedicated **Québec Compliance Pack** supporting:

- MAPAQ-related permit and register workflows
- Site-level compliance records
- Foster-capacity compliance warnings
- Dog incident and dangerous-dog case records (V2, with animal-control cases)
- CRA/Revenu Québec donation receipts
- Split receipting
- Organization-aware GST/QST configuration
- Law 25 privacy workflows
- Québec-first hosting
- French/English operation

The compliance pack remains separate from the generic core domain.

## 4.3 Support Rescue-Specific Work

Treat the following as first-class workflows rather than afterthoughts:

- Foster homes
- Volunteers
- Donations
- Community cats
- TNR/CSRM
- Partner transfers

## 4.4 Make Switching Realistic

Migration is a product capability.

The platform must provide:

- Migration from the organization's current system of record (ASM, spreadsheets, or another platform)
- Import validation
- Mapping tools
- Reconciliation
- Data exports
- No artificial data lock-in

## 4.5 Grow Without Rebuilding

The platform must support future expansion through:

- Provider-neutral integrations
- Jurisdiction models
- Configuration
- Stable domain concepts
- Multi-location organizations
- Multi-jurisdiction organizations
- Public APIs when needed

without forcing municipal or enterprise complexity into V0.

---

# 5. Core Product Principles

## 5.1 The Animal Is a Lifelong Identity

An animal is not an intake record.

The same animal may experience:

- Multiple intakes
- Foster placements
- Returns
- Transfers
- Reclaims
- Medical episodes
- Adoption
- Re-entry years later

The animal remains one identity throughout.

Example:

```text
Animal: Luna
│
├── Intake #1
│   ├── Stray
│   ├── Medical treatment
│   └── Reclaimed
│
├── Intake #2
│   ├── Owner surrender
│   ├── Foster
│   ├── Adoption
│   └── Returned
│
└── Intake #3
    └── Adoption
```

---

## 5.2 People Are Independent Entities

People are not embedded copies inside animal records.

A person may simultaneously be:

- Owner
- Adopter
- Foster
- Volunteer
- Donor
- Finder
- Surrenderer
- Witness
- Complainant
- Colony caregiver

One person record can participate in many relationships and cases.

---

## 5.3 Movements Are Historical Events

Animal location/status changes are represented through movement history.

Examples:

- Intake
- Internal move
- Foster placement
- Return from foster
- Transfer
- Adoption
- Reclaim
- Return-to-field
- Death

Adoption and foster have richer workflow records but ultimately generate animal movements.

---

## 5.4 Jurisdiction Is a First-Class Concept

A tenant may serve multiple municipalities.

A physical shelter location and a legal jurisdiction are not the same thing.

The platform therefore distinguishes:

```text
Organization
Location
Jurisdiction
```

Rules may vary by jurisdiction without duplicating the organization or animal.

---

## 5.5 Cases Are Independent of Animals

Cases may exist before an animal has been identified.

Examples:

- Lost animal
- Found animal
- Bite incident
- Welfare complaint
- Community-cat colony
- Field investigation
- Dangerous-dog file

A case may involve zero, one, or multiple animals and people.

---

## 5.6 External Identifiers Are Provider-Neutral

Examples:

- Microchip
- Municipal licence
- Emili identifier
- External shelter ID
- Veterinary ID
- Transfer-system ID

The platform owns the relationship between the animal and external identifier without making the external provider part of the core animal model.

---

# 6. Product Surfaces

The long-term product may contain several user surfaces, but they are phased.

## 6.1 Staff Application

Primary application for:

- Shelter employees
- Managers
- Medical staff
- Adoption teams
- Foster coordinators
- Administrators

This is the primary Pilot product.

## 6.2 Public Experience

Introduced progressively for:

- Adoption listings
- Adoption applications
- Foster applications
- Lost/found submissions
- Donations
- Public forms

## 6.3 Foster / Volunteer Portal

A basic foster portal ships in V1 (view assigned animals, submit updates, upload photos, record weight/observations). It is the strongest rescue-specific differentiator and directly reduces staff phone/email time.

External users can progressively:

- View assigned animals
- Submit updates
- Upload photos
- Record observations
- View tasks
- Sign documents
- Manage availability

## 6.4 Partner Portal

Future surface for:

- Rescue partners
- Veterinary partners
- Municipal users
- Transfer partners

It is not required for Pilot.

---

# 7. Animal Master Record

Each animal has a permanent master record.

Core fields include:

- Internal ID
- Shelter animal number
- Name
- Species
- Breed
- Secondary breed
- Colour
- Sex
- Reproductive status
- Birth date or estimated age
- Weight
- Size
- Coat
- Distinguishing marks
- Primary photo
- Additional photos
- Current status
- Current location
- Current jurisdiction
- Ownership/custody
- Public adoption state
- Behaviour alerts
- Medical alerts
- Legal alerts
- External identifiers
- Custom/reference values where appropriate

The animal page presents a unified timeline.

Timeline events include:

- Intakes
- Movements
- Medical events
- Foster
- Adoption
- Documents
- Payments
- Communications
- Incidents
- Transfers
- Notes

---

# 8. Intake and Outcomes

Support intake types including:

- Stray
- Owner surrender
- Return
- Transfer
- Born in care
- Seizure/custody where applicable
- Community-cat program entry
- Boarding/custody when needed

Intake records may contain:

- Date/time
- Intake type
- Reason
- Source
- Person
- Location
- Jurisdiction
- Condition
- Notes
- Documents
- Ownership information
- Related case

Outcomes include:

- Adoption
- Reclaim
- Transfer
- Return-to-field
- Death
- Other configured outcomes

Intake and outcome types map to the **Shelter Animals Count** data matrix from day one, so standard reports, benchmarks, and SAC-compatible exports work without remapping. Organizations may add sub-types beneath the standard categories.

Finalized intake/outcome records are immutable. Corrections are made through amendment entries that preserve the original record and the reason for the change.

---

# 9. Locations and Population

Support hierarchical locations.

Example:

```text
Main Shelter
├── Dog Wing
│   ├── Kennel D01
│   ├── Kennel D02
│   └── Kennel D03
│
├── Cat Room
│   ├── C01
│   └── C02
│
└── Isolation
```

Locations may include:

- Shelter
- Room
- Kennel
- Cage
- Clinic
- Isolation
- Foster home
- External veterinary clinic
- Partner shelter
- Field/community location

Operational views should answer:

- What animals are here?
- What spaces are available?
- Which animals need attention?
- Which animals have been here too long?
- Which animals have medical restrictions?

---

# 10. Medical

Pilot medical functionality focuses on daily shelter medicine rather than becoming a veterinary practice-management system.

Support:

- Medical notes
- Examinations
- Diagnoses
- Vaccinations
- Medications
- Treatments
- Tests
- Surgery
- Sterilization
- Weight
- Body condition
- Allergies
- Medical alerts
- Treatment schedules
- Due/overdue treatments

Medication administration should record:

- Medication
- Dose
- Route
- Schedule
- Prescriber where applicable
- Administrator
- Date/time
- Notes

Medical history remains part of the permanent animal timeline.

Advanced veterinary integrations come later.

---

# 11. Foster Management

Foster is a core Pilot workflow.

Support:

- Foster household
- Foster application/approval
- Capacity
- Animal restrictions/preferences
- Current animals
- Historical placements
- Placement dates
- Expected return
- Supplies
- Documents
- Agreements
- Communications
- Foster notes
- Follow-ups

Foster placement creates an animal movement.

A foster placement remains a richer record containing information beyond the movement itself.

---

# 12. Adoption

Adoption is a core Pilot workflow.

Support:

```text
Available
   ↓
Application
   ↓
Review
   ↓
Approved / Declined
   ↓
Meet / Hold
   ↓
Adoption
   ↓
Payment
   ↓
Agreement
   ↓
Animal movement
   ↓
Follow-up
```

Pilot adoption should support:

- Applicant
- Household
- Animal
- Application
- Review status
- Internal notes
- Approval
- Adoption fee
- Payment
- Agreement
- Signature
- Receipt
- Adoption movement
- Follow-up information

Do not build a generic workflow designer.

The adoption workflow is coded as product behaviour.

---

# 13. Payments

Basic payments belong in Pilot because adoption is not operationally complete without them.

Support:

- Adoption fees
- Reclaim fees where used
- Donations
- Other configured charges
- Refunds
- Payment status
- Reconciliation reference
- Receipts

Use a provider abstraction.

Initial commercial provider may be Stripe/Stripe Connect where appropriate.

The platform must not store raw card information.

Payments should support idempotent processing and webhook deduplication.

---

# 14. Donations and Donors

Donations are separate from ordinary operational payments.

A donor reuses the existing Person model.

Support in Pilot:

- Donor
- Donation amount
- Donation date
- Payment method
- Donation type
- Eligible amount
- Advantage where applicable
- Receipt eligibility
- Official receipt number
- Receipt generation
- Donation history

V1 may expand to:

- Campaign
- Tribute gift
- Recurring-gift metadata
- Donor communication preferences

Do not build a full fundraising CRM during Pilot.

Advanced fundraising may integrate with specialized systems.

---

# 15. Québec Compliance Pack

The **Québec Compliance Pack** is a major product differentiator.

It contains Québec-specific capabilities without polluting generic domain logic.

Compliance functionality must remain configurable and versioned because legal and regulatory requirements can change.

The software supports compliance workflows; it does not replace legal or regulatory advice.

---

## 15.1 MAPAQ Site Compliance

Support organization/site-level information such as:

- Permit status
- Permit number
- Issue date
- Expiration date
- Site
- Capacity
- Renewal reminders
- Supporting documents
- Inspection information

Where applicable, each regulated site can maintain its own compliance record.

---

## 15.2 Animal Register

The system should be capable of producing the animal information needed for Québec shelter/animal register obligations.

The register should be generated from normal operational records rather than requiring duplicate data entry.

Provide:

- Register view
- Date-range export
- Site-specific export
- Animal history
- Intake/outcome information
- Required identifiers
- Inspection-ready output

Compliance mappings must be versioned.

---

## 15.3 Foster Capacity Warning

The system supports configurable compliance thresholds relevant to animals kept at foster locations.

Example:

```text
Current animals at foster home: 12
Proposed placement:             +4
Projected total:                16

⚠ This placement may cross a Québec permit threshold.
Review the foster home's permit requirements before proceeding.
```

The system provides warnings and records decisions.

It does not automatically determine a person's legal status.

---

## 15.4 Dog Incident / Dangerous-Dog Records

Support Québec dog-safety case information such as:

- Bite
- Attack
- Victim
- Injury
- Incident location
- Dog
- Guardian
- Reporting source
- Municipality
- Veterinary evaluation
- Potentially-dangerous designation
- Municipal decision/order
- Conditions imposed
- Deadlines
- Notifications
- Documents
- Case timeline

The product must not incorrectly imply that every reporting duty belongs to the shelter.

These capabilities matter mainly to organizations performing municipal animal-service contracts, so they ship in **V2** alongside animal-control cases rather than in the Pilot. The Case model (section 5.5) supports them without changes.

---

## 15.5 Donation Receipts

Support receipt requirements applicable to qualified organizations.

Receipt functionality should accommodate:

- Receipt serial number
- Organization information
- Registration information
- Donor name/address
- Gift date
- Gift amount
- Description/value of advantage
- Eligible donation amount
- Receipt date
- Authorized signature
- Required federal/Québec fields

Support split receipting where applicable.

Ordinary adoption/service receipts must remain distinct from official donation receipts.

---

## 15.6 GST/QST Configuration

Do not assume every organization or transaction has identical tax treatment.

Provide:

- Organization tax status
- Configurable GST treatment
- Configurable QST treatment
- Taxable/exempt charge types
- Tax registration information
- Tax-inclusive/exclusive fee configuration where needed

Charity-appropriate defaults may be provided without hard-coding universal exemption.

---

# 16. Law 25 / Privacy Toolkit

Privacy is a product capability.

Provide a **Law 25 / Privacy Toolkit** containing:

- Privacy-impact-assessment support documentation
- Data/process inventory
- External processor inventory
- Purpose records
- Consent records where applicable
- Retention policies
- Destruction/anonymization workflows
- Access requests
- Rectification requests
- Data portability/export
- Confidentiality incident register
- Audit trail
- Privacy-protective defaults

---

## 16.1 Privacy Requests

Support:

```text
PrivacyRequest
├── Requester
├── Type
├── ReceivedAt
├── IdentityVerification
├── Deadline
├── AssignedTo
├── Records
├── Status
├── Response
└── AuditHistory
```

Request types may include:

- Access
- Rectification
- Portability
- Other configured privacy request

---

## 16.2 Retention

Retention is policy-driven.

Support:

- Record category
- Retention duration
- Trigger date
- Legal hold/exception
- Review
- Destruction
- Permitted anonymization
- Audit evidence

Scheduled retention processing should be possible without manually searching the database.

---

## 16.3 Confidentiality Incidents

Maintain an incident register containing:

- Incident
- Discovery
- Information affected
- People/records affected
- Assessment
- Actions
- Notifications
- Resolution
- Supporting documents

---

# 17. Québec-First Data Residency

Commercial production should use Québec-hosted core infrastructure by default.

Core personal-information workloads should include Québec-resident:

- Application workloads
- Primary database
- Object storage
- Backups
- Exports

External processors must be documented separately.

Québec hosting does not imply that every third-party integration processes data in Québec.

The product should maintain enough documentation to identify:

- Provider
- Purpose
- Data transmitted
- Processing/storage location where known
- Retention
- Privacy/security information

---

# 18. Community Cats / TNR

Community-cat/TNR/CSRM functionality is a first-class product capability.

A community cat remains a normal Animal.

The program connects:

```text
Animal
   ↓
Community Cat Case
   ↓
Capture
   ↓
Medical
   ├── Examination
   ├── Sterilization
   └── Vaccination
   ↓
Identification / Ear-tip
   ↓
Return-to-field Movement
   ↓
Follow-up
```

Support:

- Colony
- Colony location
- Caregiver
- Animal
- Capture date/location
- Trap information where useful
- Medical care
- Sterilization
- Vaccination
- Ear-tip
- Microchip/identifier
- Return location
- Return date
- Follow-up
- Re-entry

The system should support colony-level views and statistics.

Phasing: the domain model supports community cats from day one (Animal, Case, Location, MedicalEvent, return-to-field Movement). The dedicated TNR/colony screens ship in the Pilot **only if the pilot organization runs a TNR program**; otherwise they move to V1.

---

# 19. Lost and Found

Lost/found is V1 rather than Pilot unless required by the first pilot organization.

Support:

- Lost report
- Found report
- Animal description
- Photos
- Location
- Date/time
- Contact
- Microchip
- Matching status
- Resolution

Later matching assistance may suggest possible matches.

AI must never automatically declare ownership.

---

# 20. Volunteers

Volunteer basics enter V1.

Volunteers reuse Person records.

Support:

- Application
- Approval
- Status
- Skills
- Training
- Certifications
- Availability
- Assignments
- Hours
- Notes

Later versions may add:

- Shift scheduling
- Volunteer portal
- Training workflows
- Self-service availability
- Automated reminders

Do not build a complete workforce-management product initially.

---

# 21. Documents and Signatures

Pilot supports documents connected to operational records.

Examples:

- Adoption agreement
- Foster agreement
- Surrender agreement
- Transfer documents
- Medical documents
- Donation receipt
- Privacy documents
- Internal forms

Support:

- Template
- Generated PDF/document
- Signature
- Signer
- Timestamp
- Related entity
- Immutable finalized copy

Templates support French and English.

---

# 22. Bilingual Operation

The product is natively:

- `fr-CA`
- `en-CA`

Localization applies to both UI and business data.

Reference data should support bilingual values.

Example:

```text
IntakeReason
├── LabelFr
└── LabelEn
```

Contacts have a preferred communication language.

That preference drives:

- Email
- SMS
- Documents
- Receipts
- Agreements
- Public communication

The language of the recipient is independent from the staff user's interface language.

---

# 23. Migration

Migration is a first-class acquisition feature.

It is **not yet verified** which systems Québec shelters and rescues actually use. Many small rescues run on spreadsheets, Facebook, Google Forms, Petstablished, or Shelterluv rather than ASM.

Rule: the first dedicated importer targets **the pilot organization's current system**. ASM is the expected first target because its schema is open, but this is confirmed during pilot discovery (section 41.1), not assumed.

A strong spreadsheet/CSV import with mapping and validation may matter as much as any platform-specific importer.

The product should provide a guided **Migrate to [product]** experience. For ASM, target workflow:

```text
1. Upload ASM export
2. Analyze
3. Identify source version/format
4. Map records
5. Show unsupported/custom fields
6. Detect possible duplicates
7. Perform dry run
8. Show reconciliation report
9. Import
10. Validate totals
```

Migration should attempt to preserve:

- Animals
- People
- Intakes
- Movements
- Medical records
- Vaccinations
- Treatments
- Locations
- Payments
- Documents/media where available
- Identifiers
- Historical relationships

Generic CSV import/export remains useful but is not a substitute for a dedicated migration path for the pilot organization's system.

The switching experience is part of the product.

---

# 24. Tasks

General-purpose tasks enter V1. The Pilot relies on built-in due lists and the daily dashboard (section 41.2).

Examples:

- Medication due
- Vaccine due
- Adoption follow-up
- Foster check-in
- Document missing
- Animal needs review
- Compliance renewal
- Privacy-request deadline

Pilot may use specific task types required by built-in workflows.

Do not build a generic workflow engine.

---

# 25. Reporting

Pilot includes only reports necessary to operate and validate migration/compliance.

V1 adds a defined report library.

Examples:

- Current animals
- Intake/outcome
- Length of stay
- Population
- Foster animals
- Adoptions
- Medical due
- Donations
- Payments
- Compliance registers
- TNR activity

Do not build a generic report designer until repeated customer requirements justify one.

Complex reports may be exported to:

- CSV
- XLSX
- PDF where appropriate

---

# 26. Configuration Philosophy

Prefer configuration over customer-specific forks.

However, do not turn every behaviour into a configuration engine.

Pilot/V1 configuration should focus on:

- Species
- Breeds
- Colours
- Reasons
- Locations
- Fees
- Payment types
- Document templates
- Email/SMS templates
- Selected statuses
- Jurisdiction settings
- Compliance settings

Core workflows remain product-defined.

For example:

```text
Adoption workflow = product code

Adoption fee = configuration
Adoption reasons = configuration
Adoption agreement = template
```

Only generalize a workflow after multiple real customers demonstrate the same customization need.

---

# 27. No Generic Engines in Early Versions

The following are explicitly deferred:

- Generic workflow designer
- Generic automation builder
- Generic report builder
- Generic form builder
- Large configuration studio

Pilot/V1 uses:

```text
Workflows   → coded
Reports     → predefined
Automation  → coded
Forms       → predefined
```

with configurable reference data, fees, templates, and policies.

---

# 28. Provider-Neutral Integrations

External providers are replaceable.

Conceptual interfaces include:

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

The product must remain usable when a provider is unavailable.

---

# 29. Licensing

Licensing is not a Pilot core workflow unless required by a pilot customer.

Initial interoperability should support file-based exchange.

Conceptually:

```text
LicensingProvider
└── CsvLicensingProvider
```

Later providers may include:

```text
EmiliProvider
MunicipalProvider
OtherVendorProvider
NativeLicensingProvider
```

Do not assume access to any particular external API.

**Emili is a connector, never a dependency.**

Native licence issuing belongs in a later phase.

---

# 30. Licensing-Provider Disclosure

When personal information is intentionally sent to a licensing provider or municipality, the workflow should clearly disclose that transfer to the user where appropriate.

Maintain a disclosure record:

```text
Disclosure
├── Person
├── Recipient
├── Purpose
├── DataShared
├── LegalBasis / ConsentWhereApplicable
├── DisclosureTextVersion
├── Timestamp
└── Result
```

Do not assume consent is the legal basis for every disclosure.

The platform records the applicable basis/configuration instead.

---

# 31. Tenant Data Boundaries

Tenant isolation is a core product promise.

Person records are tenant-private.

There is no hidden platform-wide adopter, foster, volunteer, donor, or citizen database accessible across customers.

Example:

```text
Organization A
└── Person #123

Organization B
└── Person #987
```

Even if both records represent the same real-world person, they remain independent tenant records unless an explicit cross-organization mechanism exists.

---

# 32. Cross-Organization Sharing

Cross-tenant sharing must be:

- Explicit
- Authorized
- Purpose-limited
- Minimal
- Logged

Organizations do not gain arbitrary access to another tenant's records.

Transfers should use a transfer package.

Example:

```text
Organization A
       │
       ▼
Transfer Package
       │
       ▼
Organization B
```

The receiving organization imports the permitted information into its own tenant context.

---

# 33. Transfer Packages

A lightweight transfer capability belongs in V2.

A transfer package may contain:

- Animal identity
- Photos
- Microchip
- Vaccination history
- Relevant medical history
- Behaviour information
- Documents
- Transfer metadata
- Chain of custody

Initial model:

```text
Export
   ↓
Transfer package
   ↓
Import
```

A direct cross-platform transfer network comes later.

---

# 34. Public API

The internal application uses REST/OpenAPI from the beginning.

This does **not** mean the product promises a public developer API in Pilot/V1.

Do not prematurely freeze a public contract.

Public API becomes a V2 capability when actual integrations require it.

V2 may introduce:

- `/api/v1`
- API credentials
- OAuth where justified
- Scopes
- Rate limits
- Versioning
- Developer documentation
- Webhooks

---

# 35. Accessibility

Public-facing experiences target **WCAG 2.2 AA**.

This includes:

- Adoption listings
- Adoption applications
- Foster applications
- Donation flows
- Lost/found forms
- Public forms
- External portals
- Municipal public experiences when introduced

Accessibility should include:

- Keyboard operation
- Screen-reader semantics
- Focus management
- Appropriate colour contrast
- Accessible validation/errors
- Accessible forms
- Text alternatives
- Responsive layouts
- Reduced-motion considerations

Generated public documents should also consider accessibility where practical.

---

# 36. Search

Staff need fast global search.

Search should cover:

- Animal name
- Animal number
- Microchip
- Licence
- Person
- Phone
- Email
- Address
- Case
- Application
- Kennel/location

Search should handle accents appropriately.

Example:

```text
Éclair
eclair
```

should be capable of finding the same record.

Identifiers such as microchips and phone numbers require exact/normalized lookup rather than only natural-language search.

---

# 37. Audit

Sensitive changes must be auditable.

Audit information includes:

- Tenant
- Actor
- Action
- Record
- Timestamp
- Source
- Relevant before/after values
- Correlation information

Important events include:

- Animal changes
- Ownership changes
- Medical changes
- Adoption decisions
- Payments/refunds
- Donation receipts
- Privacy requests
- Permission changes
- Data exports
- Cross-organization sharing
- Integration disclosures

Audit records must not be casually editable by ordinary users.

---

# 38. Data Ownership and Portability

Customers own their operational data.

The product must support export of:

- Animals
- People
- Movements
- Medical history
- Foster history
- Adoption history
- Donations
- Payments
- Documents
- Audit information where appropriate

Data portability is a product principle, not merely a cancellation feature.

Avoid vendor lock-in through proprietary inaccessible formats.

---

# 39. AI Guardrails

AI is not required for Pilot or V1.

AI capabilities are added only when they solve a demonstrated workflow problem.

AI is disabled by default per tenant unless a feature specifically requires activation.

By default:

> Personal information is not sent to external AI providers.

Before enabling AI functionality involving personal information:

- Provider must be documented
- Data residency must be understood
- Privacy assessment requirements must be addressed
- Contractual requirements must be addressed
- Only necessary data is transmitted
- Tenant administrator must explicitly enable the capability
- Appropriate disclosure must exist

Prefer Québec-resident processing when feasible.

AI outputs should preserve provenance/source context where relevant.

AI may assist with:

- Drafting animal descriptions
- Summarizing notes
- Suggesting duplicate records
- Translating content
- Searching records
- Drafting communication
- Categorizing non-critical information

AI must **not autonomously make**:

- Adoption decisions
- Medical decisions
- Euthanasia decisions
- Dangerous-dog decisions
- Animal-welfare enforcement decisions
- Legal determinations

Humans remain responsible for consequential decisions.

---

# 40. Commercial Model Assumptions

Exact pricing is intentionally not defined yet.

The likely primary model is:

> Subscription per organization.

Potential pricing dimensions include:

- Number of locations
- Annual intake/activity
- Optional modules
- Storage
- SMS usage
- Premium integrations

Do **not** price by staff seats. Rescues run on volunteers and fosters, and ASM includes unlimited users; seat pricing would penalize exactly the collaboration the product encourages.

Price anchor: ASM hosted costs roughly **CAD $455/year** for unlimited users. Small rescues will compare against that. Entry pricing must be defensible against it, with higher tiers justified by compliance, payments, and migration value.

Potential future revenue sources include:

- Payment platform fees
- Premium integrations
- Municipal functionality
- Migration services
- Advanced support

Avoid business models that discourage desirable animal-welfare outcomes.

In particular, avoid relying primarily on:

- Per-adoption charges
- Per-foster charges
- Per-volunteer charges
- Data-export fees
- Artificial data lock-in

Payment platform fees are effectively charged per adoption or donation. If used, they must be transparent, capped or small relative to processor costs, and optionally covered by the payer rather than deducted from the organization.

Pricing experiments must be validated with real organizations before becoming architectural assumptions.

---

# 41. Pilot Scope

The Pilot is the smallest version intended to replace the current system of record (ASM or otherwise) for one real organization.

## 41.1 Pilot Prerequisites (Discovery Gate)

Before Pilot scope is frozen:

1. Identify the pilot organization.
2. Confirm its current system of record and how adoptions/applications arrive today.
3. Confirm whether it runs a TNR program.
4. Confirm whether it issues official donation receipts (registered charity) or only ordinary receipts.
5. Confirm which Québec compliance outputs it is actually asked to produce.

Items marked *conditional* below are included or dropped based on these answers.

## 41.2 Pilot Includes

### Core Records

- Animals
- People
- Locations
- Intake
- Outcomes
- Movement history

### Medical Basics

- Exams/notes
- Vaccinations
- Medications
- Treatments
- Sterilization
- Medical alerts
- Due/overdue lists (vaccines, treatments, holds)

### Daily Dashboard

- What is due today (medical, holds, follow-ups)
- Animals without microchip, long-stay animals, foster returns due
- Current population by location

### Placement

- Foster
- Adoption

### Adoption Intake

- Minimal public adoptable-animals listing
- Embeddable listing widget
- Online adoption application form
- Share links with social previews

Without these, applications must be retyped by staff or the previous system must stay in use for publishing, which defeats the Pilot goal.

### Communications

- Transactional email (agreements, receipts, application confirmations) in the recipient's language

### Financial Basics

- Adoption payments
- Other basic fees
- Donations
- Receipts
- Refunds where required

### Documents

- Document templates
- Generated documents
- Signatures

### Québec Compliance Pack

- MAPAQ site information
- Animal-register support
- Foster threshold warnings
- Donation receipt support (official receipts *conditional* on registered-charity status)
- GST/QST configuration
- Québec-first hosting requirements

### Law 25 / Privacy Minimum

- Consent records
- External processor inventory
- Privacy request log (access/rectification) with deadlines
- Confidentiality incident register
- Retention policies configured (automated purge processing follows in V1)
- Audit trail

### Community Cats (*conditional*)

Included only if the pilot organization runs a TNR program:

- TNR/community-cat records
- Colony/location
- Capture
- Medical
- Sterilization
- Return-to-field

### Migration

- Dedicated importer for the pilot organization's current system (ASM expected)
- Generic CSV/spreadsheet import/export with mapping
- Reconciliation

### Platform Foundations

- French/English
- Permissions
- Audit
- Privacy
- Tenant isolation

---

# 42. V1 Scope

After a successful Pilot, add:

- Richer public animal profiles and listing filters
- Foster application forms
- Basic foster portal (section 6.3)
- Tablet kennel rounds with QR kennel cards (online; offline later)
- Community-cat/TNR screens if not in Pilot
- Automated retention/purge processing
- Lost/found
- Core reporting library
- Operational tasks
- Volunteer basics
- Improved donor history
- Donation campaigns/basic categorization
- Communication improvements
- Additional operational dashboards

V1 remains focused on shelter/rescue operations.

---

# 43. V2 Scope

V2 expands into broader organization and municipal capabilities.

Potential V2 features:

- Animal-control cases
- Dog incident / dangerous-dog records (section 15.4)
- Field-service workflows
- Municipal contracts
- Licensing connectors
- Transfer-package import/export
- Public API
- Webhooks
- Partner integration foundations
- Advanced reporting
- Repeated-workflow automation
- Deeper volunteer functionality
- Deeper fundraising functionality

Generic engines should still require demonstrated customer demand.

---

# 44. V3 Scope

Potential V3 capabilities:

- Native municipal pet licensing
- Dispatch
- Direct partner transfer network
- Veterinary partner portal
- Advanced offline field application
- Integration marketplace
- Enterprise identity
- Advanced analytics
- Carefully scoped AI assistance

V3 represents platform expansion rather than the initial product.

---

# 45. Explicit Non-Goals for Pilot

Pilot does not attempt to build:

- Complete municipal dispatch software
- Native municipal licensing
- Generic workflow designer
- Generic automation builder
- Generic report builder
- Generic form builder
- Full fundraising CRM
- Veterinary practice-management software
- Accounting software
- Public developer platform
- Integration marketplace
- Full offline synchronization
- Native mobile application
- AI decision-making
- Nationwide regulatory engine
- Customer-specific code forks

---

# 46. Pilot Success Criteria

Pilot success is measured by real operational adoption rather than number of completed features.

The primary criterion is:

> **At least one Québec shelter or rescue operates its daily animal-management workflow in the platform for 30 consecutive days without its previous system (ASM or otherwise) being used as its system of record.**

During that period:

- Active animals are represented accurately
- New intakes are entered in the platform
- Animal movements are recorded
- Medical treatments are recorded
- Foster placements are managed
- Adoptions are completed
- Required documents are generated
- Payments and receipts reconcile
- Required compliance records can be produced
- No critical tenant-isolation incident occurs
- No critical data loss occurs
- Staff can perform routine workflows without developer intervention
- The previous system is used only for historical verification if required, not daily operations
- New adoption applications arrive through the platform, not the previous system

Additional pilot metrics should measure:

- Time to complete common workflows
- Staff-reported friction
- Number of support interventions
- Import accuracy
- Data-quality problems
- Failed payments
- Missing compliance information
- Application reliability

---

# 47. Product Development Rule

For every proposed feature, ask:

> **Does the first pilot organization need this to stop using its current system?**

If yes, it may belong in Pilot.

If no, ask:

> **Will multiple customers need this soon after Pilot?**

If yes, consider V1/V2.

Otherwise, defer it.

This rule is intended to protect the project from becoming:

> Emili + Shelterluv + ASM + dispatch software + veterinary software + fundraising CRM

before a single organization successfully uses it.

---

# 48. Guardrails

The following product guardrails are permanent unless deliberately reconsidered.

1. The animal remains a lifelong identity.
2. Person, Animal, Case, Jurisdiction, Location, Movement, and MedicalEvent remain separate domain concepts.
3. Tenant data is isolated.
4. Person records do not silently cross tenants.
5. Cross-organization sharing is explicit and logged.
6. External vendors remain replaceable.
7. Emili is a connector, not a dependency.
8. Québec-specific requirements belong in a compliance/rule layer rather than polluting generic domain logic.
9. French and English apply to business data and communication, not only interface strings.
10. Privacy is designed into workflows.
11. Sensitive changes are auditable.
12. Customers can export their data.
13. Configuration is preferred over customer-specific forks.
14. Generic engines are not built before repeated customer demand demonstrates the abstraction.
15. Public APIs are introduced when external consumers exist.
16. AI receives no personal information by default.
17. AI never autonomously makes consequential animal-welfare, medical, adoption, legal, dangerous-dog, euthanasia, or enforcement decisions.
18. Accessibility is part of product quality.
19. Compliance functionality supports organizations but does not pretend the software itself can make legal determinations.
20. The product is judged by successful daily use, not by feature count.

---

# 49. Final Product Direction

The product starts as:

> **A modern, bilingual shelter and rescue management system built for Québec.**

Its initial competitive strength comes from the combination of:

```text
Excellent shelter operations
        +
Foster and adoption
        +
Community cats / TNR
        +
Payments and donations
        +
Québec Compliance Pack
        +
Law 25 / privacy foundations
        +
Migration from the current system
        +
French / English
```

The initial objective is not to serve every municipality or solve every animal-welfare workflow.

The objective is to make one real Québec shelter or rescue willing and able to stop using its legacy shelter-management system.

Once that is achieved repeatedly, the same underlying domain can expand toward:

```text
Shelters / Rescues
        ↓
Municipal animal services
        ↓
Licensing integrations
        ↓
Field operations
        ↓
Partner transfers
        ↓
Veterinary collaboration
        ↓
Broader Canadian animal-welfare platform
```

That expansion should happen through real customer demand rather than speculative platform engineering.

The core moat is not the number of modules.

It is the combination of:

- A correct animal-welfare domain model
- Québec-specific operational understanding
- Excellent migration
- Bilingual workflows
- Privacy and compliance foundations
- Foster/rescue/TNR support
- Data portability
- Provider-neutral integrations
- A product simple enough for shelter staff to use every day