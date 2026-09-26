# Architecture Decision Records

One decision per file. Index below. Use `/adr` to add one.

## Index

| # | Title | Status |
|---|---|---|
| — | — | — |

## Backlog (from architecture §32 — write during M0)

Write these as short ADRs so future sessions know *why*, not just *what*:

1. Modular monolith with vertical modules
2. PostgreSQL as the only datastore (no Redis in V1)
3. Shared database + `TenantId` + EF filters + RLS
4. Tenant propagation via `SET LOCAL app.tenant_id` per transaction, fail closed
5. **DbContext strategy** (single composed DbContext with schema-per-module vs one DbContext per module) — decide in M0-8; affects cross-module transactions (§7.1)
6. Handlers as plain classes, Minimal APIs, no MediatR
7. ASP.NET Identity + same-site cookie auth; no OpenIddict in V1
8. Orval for the TypeScript client
9. One frontend runtime (Vite); social previews via backend share endpoint
10. Hangfire with PostgreSQL storage
11. Crypto-shredding for personal data in append-only stores
12. AWS `ca-central-1`, multi-AZ, no cross-region replication; OpenTofu
13. Bilingual reference data as paired columns (`LocalizedText`)
14. Search in PostgreSQL (`unaccent` + stemming + trigram)
15. Stripe Connect account type (Standard vs Express) — decide before payments work
