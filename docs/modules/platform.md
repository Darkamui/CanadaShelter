# Module: Platform

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- Target ownership (architecture §6.9): tenant provisioning, authentication integration, staff memberships, external identities, permissions, privacy workflows, consent, retention, audit access, integrations, webhooks, imports/exports, feature flags, SaaS administration.
- **Today (M1):**
  - `Organization` (the tenant): a global table with no personal data.
  - `TenantSetting` (the sample tenant-owned table).
  - `OrganizationProvisioner` (the platform-admin path).
  - `GET /api/platform/ping`.
- PostgreSQL schemas: `platform` (this module), `audit` (the BuildingBlocks audit tables, ADR 0016) and `hangfire` (job storage, ADR 0010).

## Does not own

- Host plumbing lives in `Shelter.Host/Composition` and `Middleware`:
  - health checks, ProblemDetails, correlation IDs and logging;
  - tenant resolution;
  - Hangfire setup.
- The shared mechanisms live in `BuildingBlocks`:
  - tenancy, persistence/RLS, audit, crypto-shredding, jobs, `LocalizedText`/`ReferenceList`;
  - the classification taxonomy and log redaction (`Logging`).
- Reference lists live in their owning modules (Animals `species`, Movements `intake_reason`, ADR 0017).

## Platform foundations (M1)

- **Tenancy:**
  - Entities implement `ITenantOwned`. `TenantStampingInterceptor` stamps `TenantId` and rejects cross-tenant or tenant-less writes.
  - A global query filter hides everything when no tenant is set.
  - `TenantContext` is scoped. `DevelopmentHeaderTenantResolver` (`X-Tenant-Id`) is registered only in Development; everywhere else resolution fails closed.
- **RLS (ADR 0004):**
  - `UnitOfWorkEndpointFilter` wraps every tenant request in a transaction.
  - `TenantTransactionInterceptor` runs `SET LOCAL app.tenant_id` when the transaction starts.
  - `TenantCommandGuardInterceptor` throws on a tenant command that runs outside a transaction.
  - In migrations, `EnableTenantRls(schema, table)` turns RLS on and forces it, then creates a `tenant_isolation` policy on `platform.current_tenant_id()` plus a `platform_admin_access` policy. `GrantRuntime` / `GrantRuntimeSchemaUsage` grant the runtime role its rights.
- **Roles:**
  - `shelter_app` is the runtime role.
  - `shelter_migrator` owns the DDL.
  - `shelter_platform_admin` has NOBYPASSRLS; it crosses tenants only through its explicit policy, and only via `PlatformAdminDbContextFactory`. An architecture test restricts that factory to this module.
- **Jobs (ADR 0010):**
  - `ITenantJob<TArgs>` jobs are started with `ITenantJobScheduler.Enqueue(tenantId, args)`.
  - `TenantJobRunner` checks the tenant, restores the tenant and audit context, and runs inside the UoW.
  - Job arguments hold **IDs only, never personal data**.
  - The dashboard is mapped only in Development, for local requests only. `Jobs:ServerEnabled` turns the server off.
- **Audit (architecture §17):**
  - `AuditSaveChangesInterceptor` writes `audit.audit_event` rows in the same transaction; the runtime role has INSERT and SELECT only.
  - Fields are classified with `IsPersonalData()` / `IsNonPersonalData()`. **Unclassified values are never written**; only the field name is recorded.
  - `IAuditWriter` is the entry point for non-EF paths.
- **Crypto-shredding (ADR 0016):**
  - `HasAuditSubject(...)` names the person. Personal values are stored as `{"$enc":"v1:…"}` under a per-subject key in `audit.person_data_key`, and that key is wrapped by `IKeyProvider`.
  - `ISubjectShredder` tombstones the key (and audits the shred). `IAuditReader` decrypts values, or marks them unrecoverable after a shred.
  - Startup fails when no key provider is configured.
- **Reference data (ADR 0013, 0017):**
  - Global table plus a tenant `<list>_override` table, merged by `ReferenceList.Merge`.
  - The API shape is `{ code, label: { fr, en } }`, and the admin app picks the label with `useLocalize`.

## Contracts (what other modules may use)

- None yet (`Shelter.Modules.Platform.Contracts` is empty). The shared mechanisms above are in `BuildingBlocks`.

## Events

- None yet.

## Invariants

- Tenant isolation:
  - Stamping and cross-tenant rejection: `TenantStampingTests`.
  - RLS policies: `TenantRlsTests`.
  - Harness-based isolation: `TenantIsolationTests`.
  - Pooled connections: `PooledConnectionTests`.
  - Unit of work: `UnitOfWorkTests`.
  - All of these run as `shelter_app`.
- Jobs cannot read another tenant's data, and a job without a tenant fails: `TenantJobTests`, `HangfireTests`.
- Audit rows are append-only for the runtime role, with before/after values and actor: `AuditCaptureTests`, `RuntimeRoleTests`.
- Personal values are unrecoverable after a shred: `CryptoShreddingTests`. Person-linked entities are fully classified: `ClassificationRuleTests`.
- Reference overrides stay inside their tenant, and system values are read-only: `ReferenceDataEndpointTests`.

## Permissions

- None yet. `ping` and the reference-list endpoints are `.AllowAnonymous()` until identity arrives (M2).

## Key files

- `Domain/Organization.cs`, `Domain/TenantSetting.cs`, `Persistence/PlatformModelContributor.cs`, `Provisioning/OrganizationProvisioner.cs`.
- `Features/Ping/PingEndpoint.cs`: `GET /api/platform/ping` (`GetPlatformPing`).
- Migrations: `backend/Shelter.Migrations` (one assembly, ADR 0005).
- Test harness: `backend/Tests/Shelter.Testing` (`PostgresDatabase`, `TenantHarness.AssertIsolatedAsync`).

## Open questions / TODO

- **M2:**
  - Add an authorization `FallbackPolicy` and permissions for ping, the reference lists, `IAuditReader` and the Hangfire dashboard.
  - Replace the dev header resolver.
- Known gaps (from the reviewer):
  - `AuditRecord.Metadata` is not classified, so callers must keep personal data out of it.
  - `EntityId` is assumed to be non-personal.
  - Nothing yet stops code from logging decrypted `AuditEntry` values; treat them as personal.
- `appsettings.Development.json` holds a **dev-only** audit master key. Production needs a real `IKeyProvider`; the AWS KMS provider is a stub.
- The Hangfire tests run serially because they share the server's storage.
- Local databases created before M1 need `docker compose … down -v` once, for the `shelter_platform_admin` role.
