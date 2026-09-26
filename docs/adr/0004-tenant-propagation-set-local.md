# 0004. Tenant propagation via `SET LOCAL app.tenant_id` per transaction, fail closed

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

RLS policies (ADR 0003) need to know the current tenant inside PostgreSQL. Connections are pooled. A session-level setting would outlive the request and hand the previous tenant's identity to the next one (§8.3).

## Decision

- **The setting:** RLS policies read `current_setting('app.tenant_id', true)`.
- **The interceptor:** an EF Core connection/transaction interceptor issues `SET LOCAL app.tenant_id = …` at the start of every unit of work. `SET LOCAL` is transaction-scoped.
- **Explicit transactions:** every tenant-scoped operation runs inside one. Without a transaction, `SET LOCAL` has no effect.
- **Fail closed:** when the setting is missing or empty, policies match **no rows**, never all rows.
- **Other entry points:** Hangfire jobs, imports and integration handlers restore `TenantId` from their payload, validate it, and go through the same interceptor.
- **Mandatory test:** tenant A then tenant B, sequentially, over the **same pooled connection**, with no leak.

## Alternatives considered

- **Session-level `SET` / `set_config(…, false)`**: leaks across pooled connections. Resetting on return to the pool is fragile, and a single missed reset is a breach.
- **One database role per tenant**: needs role management at scale and destroys connection pooling.
- **Passing the tenant as a parameter to every query or view**: easy to forget, and it doesn't protect raw SQL.

## Consequences

- Positive: the tenant can't outlive its transaction. A forgotten context produces empty results, not a leak.
- Negative / accepted trade-offs:
  - Even reads need an explicit transaction.
  - Reads that bypass EF (Dapper/raw SQL) must use the same connection and transaction, or set the tenant themselves.
  - "No rows" bugs can look like missing data rather than failing loudly. Tests must cover them.
- Follow-ups: M1 implements the interceptor and the same-connection test. Its interaction with the DbContext layout is decided in ADR 0005.
