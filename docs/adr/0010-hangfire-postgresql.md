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

## Addendum (2026-09-27, M1-4)

- **Schema and privileges:** the `hangfire` schema is created by a migration (`AddHangfireSchema`), which grants the runtime role `shelter_app` `USAGE, CREATE` on that schema only. Hangfire then installs and upgrades its own tables as `shelter_app` (`PrepareSchemaIfNecessary`), so a Hangfire upgrade needs no hand-ported migration.
  - **Accepted risk:** this is the one exception to "runtime roles have no DDL" (architecture §8.3). A compromised runtime connection could create objects in `hangfire`. It gains no access to tenant data: `shelter_app` has no new privileges on other schemas, and anything it creates runs with its own rights.
  - **Revisit before production:** install the schema as the migrator, set `PrepareSchemaIfNecessary = false`, and revoke `CREATE`.
- **Tenant restore:** implemented as `TenantJobRunner<TJob, TArgs>`, not a Hangfire filter.
  - `ITenantJobScheduler.Enqueue` takes the tenant from the scheduling scope's `TenantContext`, never from a parameter.
  - The runner opens a new DI scope, sets the tenant, and runs the job inside a `UnitOfWork`.
  - A payload without a tenant throws before any data access.
- **Dashboard:** until staff authentication and a platform-admin policy exist (M2), `/hangfire` is mapped only in Development and only for local requests. It does not exist in other environments.

## Addendum (2026-09-27, M2-8): global jobs

- **Why.** Some work belongs to no tenant: platform-wide tables such as accounts and `platform.invitation_token`. Running it as a tenant job would mean inventing a tenant.
- **Pattern.** An `IGlobalJob<TArgs>` job runs through `GlobalJobRunner<TJob, TArgs>`:
  - it gets a new DI scope, an audit source of `job:<name>` and one unit of work, like a tenant job;
  - it never sets a tenant, so RLS hides every tenant-owned row.
  - Enqueue with `IGlobalJobScheduler`, and register the job with `AddGlobalJob`. Tenant data stays in `ITenantJob`.
- **Recurring jobs.** Modules declare them with `AddRecurringGlobalJob<TJob, TArgs>(id, cron, args)`. A host that runs a job server writes the schedules to Hangfire at startup. Hosts that only enqueue (tests, the OpenAPI export) leave them alone.
- **Arguments.** The rule is unchanged: IDs and codes only, never personal data. The first two jobs:
  - `SendPasswordResetEmailJob` carries a user ID or nothing.
  - `PurgeExpiredInvitationTokensJob` runs hourly with no arguments.
