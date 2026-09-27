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
  - `TenantContext` is scoped. Since M2-2 the only resolver is `MembershipTenantResolver` (ADR 0018): the session's active organization, re-checked against an active `staff_membership` on every request. The M1 dev header (`X-Tenant-Id`) is gone; integration tests sign in through a test-only scheme (`TestAuthentication`).
  - `UserContext` (scoped) holds the signed-in account; `TenantTransactionInterceptor` also runs `SET LOCAL app.user_id`, read by `platform.current_user_id()` and the `self_read` policy (`EnableSelfRead`).
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
- Permissions (M2-3): every endpoint declares a permission, the session policy or the operator policy, or is allowlisted anonymous. Every permission endpoint returns 403 to a member without it. An undeclared endpoint needs a member: `EndpointAuthorizationTests`. The role → permission mapping: `PermissionCatalogTests`.
- Staff (M2-3): the organization keeps at least one active administrator (409, serialized by `FOR UPDATE` on its administrators). Role changes are audited and apply on the next request. Other organizations' memberships return 404: `StaffManagementTests`.
- Memberships (M2-2): no tenant without an active membership, suspension effective on the next request, organization switch only among own memberships, self-read limited to own rows and SELECT: `MembershipTests`. `app.user_id` never outlives its transaction: `PooledConnectionTests`.

## Permissions

- Model (M2-3, ADR 0018):
  - Each module declares `PermissionDefinition(name, Read/Write, Sensitive)` in its `Authorization/` folder and registers them with `services.AddPermissions(...)`. `PermissionCatalog` rejects duplicate names.
  - System roles, stored in `staff_membership.role_keys`: `administrator` = all, `staff` = every non-sensitive permission, `read_only` = every non-sensitive read. Unknown keys grant nothing.
  - `MembershipTenantResolver` resolves the tenant and the permissions from one membership row per request. They are exposed as `IPermissionContext` and returned sorted in `GET /api/platform/session` (`permissions`).
- Endpoint rules:
  - `.RequirePermission(name)` = active member of the active organization + that permission.
  - `.RequireSession()` = signed in, no organization needed.
  - The fallback policy (every endpoint that declares nothing, including unknown routes) = authenticated active member.
  - Anonymous endpoints are only those on the allowlist in `EndpointAuthorizationTests`: ping, `session/antiforgery`, `session/login`, health.
  - The Hangfire dashboard (`/hangfire`, all environments) needs the `PlatformOperator` policy: `shelter:operator=true` and `amr=mfa`.
- Platform permissions:
  - `platform.staff.read` (sensitive): list staff, with colleagues' emails.
  - `platform.staff.manage` (sensitive): change roles, suspend, reactivate. Sensitive so that no non-admin role can grant itself anything.
  - `audit.read` (sensitive): checked by `AuditReader` itself (`AuditReadDeniedException`).

## Key files

- `Domain/Organization.cs`, `Domain/TenantSetting.cs`, `Persistence/PlatformModelContributor.cs`, `Provisioning/OrganizationProvisioner.cs`.
- `Features/Ping/PingEndpoint.cs`: `GET /api/platform/ping` (`GetPlatformPing`).
- `Authorization/PlatformPermissions.cs`; the shared model is in `BuildingBlocks/Authorization/Permissions.cs` and `EndpointAuthorizationExtensions.cs`.
- `Features/Staff/StaffEndpoints.cs`, under `/api/platform/staff`:
  - `GET` (`ListPlatformStaff`)
  - `PUT /{membershipId}/roles` (`ChangePlatformStaffRoles`)
  - `POST /{membershipId}/suspend` (`SuspendPlatformStaff`) and `POST /{membershipId}/reactivate` (`ReactivatePlatformStaff`)
- Migrations: `backend/Shelter.Migrations` (one assembly, ADR 0005).
- Test harness: `backend/Tests/Shelter.Testing` (`PostgresDatabase`, `TenantHarness.AssertIsolatedAsync`).

## Open questions / TODO

- **M2:**
  - M2-4: the tenant and fallback policies will also require MFA for administrators.
- Memberships created before M2-3 have no roles, and so no permissions. The development seed gives the demo account `administrator` again.
- Known gaps (from the reviewer):
  - `AuditRecord.Metadata` is not classified, so callers must keep personal data out of it.
  - `EntityId` is assumed to be non-personal.
  - Nothing yet stops code from logging decrypted `AuditEntry` values; treat them as personal.
- `appsettings.Development.json` holds a **dev-only** audit master key. Production needs a real `IKeyProvider`; the AWS KMS provider is a stub.
- The Hangfire tests run serially because they share the server's storage.
- Local databases created before M1 need `docker compose … down -v` once, for the `shelter_platform_admin` role.
