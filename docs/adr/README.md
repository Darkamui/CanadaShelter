# Architecture Decision Records

One decision per file. Index below. Use `/adr` to add one.

## Index

| # | Title | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith with vertical modules | Accepted |
| [0002](0002-postgresql-only-datastore.md) | PostgreSQL as the only datastore (no Redis in V1) | Accepted |
| [0003](0003-shared-database-tenantid-rls.md) | Shared database + `TenantId` + EF filters + RLS | Accepted |
| [0004](0004-tenant-propagation-set-local.md) | Tenant propagation via `SET LOCAL app.tenant_id` per transaction, fail closed | Accepted |
| [0005](0005-dbcontext-strategy.md) | DbContext strategy: single composed DbContext, schema per module | Accepted |
| [0006](0006-plain-handlers-minimal-apis.md) | Handlers as plain classes, Minimal APIs, no MediatR | Accepted |
| [0007](0007-identity-cookie-auth.md) | ASP.NET Identity + same-site cookie auth; no OpenIddict in V1 | Accepted |
| [0008](0008-orval-typescript-client.md) | Orval for the TypeScript client | Accepted |
| [0009](0009-single-frontend-runtime.md) | One frontend runtime (Vite); social previews via backend share endpoint | Accepted |
| [0010](0010-hangfire-postgresql.md) | Hangfire with PostgreSQL storage | Accepted |
| [0011](0011-crypto-shredding.md) | Crypto-shredding for personal data in append-only stores | Accepted |
| [0012](0012-aws-ca-central-1-opentofu.md) | AWS `ca-central-1`, multi-AZ, no cross-region replication; OpenTofu | Accepted |
| [0013](0013-localized-text-paired-columns.md) | Bilingual reference data as paired columns (`LocalizedText`) | Accepted |
| [0014](0014-postgresql-search.md) | Search in PostgreSQL (`unaccent` + stemming + trigram) | Accepted |
| [0015](0015-backend-project-and-test-layout.md) | Backend project and test layout | Accepted |
| [0016](0016-audit-key-management.md) | Audit key management for crypto-shredding | Accepted |
| [0017](0017-reference-data-overrides.md) | Reference data: global system tables plus per-tenant override tables | Accepted |
| [0018](0018-identity-authorization-model.md) | Identity and authorization model (global accounts, membership self-read, permissions, MFA) | Accepted |

## Backlog

- Stripe Connect account type (Standard vs Express) — decide before payments work (architecture §15.1). Takes the next free number when written.
