# 0010. Hangfire with PostgreSQL storage

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

V1 needs work that is scheduled, recurring and retryable: reminders, scheduled communications, report and export generation, retention purges, integration imports, and webhook retries (§13). No broker or Redis is allowed in V1 (ADR 0002). Every tenant job must run under the right tenant (§8.5).

## Decision

- **Engine:** **Hangfire** with **PostgreSQL storage** in the same database, in its own schema. Workers run in the Host process.
- **Tenant context:** every tenant-specific job carries an explicit `TenantId` in its arguments. A job filter or base class restores and validates `TenantContext` before any data access, then goes through the `SET LOCAL` path (ADR 0004).
- **Arguments:** job arguments carry IDs only, never personal data. They're persisted and visible in the dashboard.
- **Dashboard:** exposed only to platform admins, behind authorization.

## Alternatives considered

- **Quartz.NET**: strong scheduling, but less built-in retry/queue semantics and dashboard.
- **Plain `BackgroundService` + custom table**: we'd rebuild retries, scheduling, concurrency control and monitoring.
- **Cloud queue (SQS) + workers**: new infrastructure, harder local dev, and an extra residency review.

## Consequences

- Positive:
  - Mature retries, scheduling and dashboard.
  - Job storage is transactional with our data.
  - No new infrastructure.
- Negative / accepted trade-offs:
  - Polling load on PostgreSQL.
  - Job arguments are serialized type names, so refactoring job classes needs care.
  - Workers share the web process's resources until we split them out.
- Follow-ups: when jobs arrive, add the tenant job filter and a test that a job without a valid `TenantId` refuses to run.
