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
  - `TenantContext` is scoped. Since M2-2 the only resolver is `MembershipTenantResolver` (ADR 0018): the session's active organization, re-checked against an active `staff_membership` on every request. The M1 dev header (`X-Tenant-Id`) is gone; integration tests sign in through a test-only scheme (`TestAuthentication`). `SignInAs` counts as signed in with MFA (`amr=mfa`) unless the test passes its own `amr` claim.
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
- MFA (M2-4): an administrator or operator without MFA reaches only the session endpoints, and full access follows enrollment in the same session. Two-step login, a recovery code works once and is stored hashed, disabling needs a code, other staff are unaffected, the bypass flag is ignored outside Development: `MfaTests`, `MfaEnforcementTests`.
- Invitations (M2-5): accepting creates the membership with the invited roles, once. Expired, revoked, replaced and used links are rejected. A link only reaches its own organization. An existing account must be signed in as itself: `InvitationTests`.
- Password reset (M2-5): a link works once, and forgot/reset answer the same whether or not the account exists: `PasswordResetTests`.
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
  - Both the permission policies and the fallback also require MFA when the session must use it (see MFA below). `.RequireSession()` does not, so enrollment stays reachable.
  - Anonymous endpoints are only those on the allowlist in `EndpointAuthorizationTests`: ping, `session/antiforgery`, `session/login`, `session/login/mfa`, `session/password/forgot`, `session/password/reset`, `invitations/lookup`, `invitations/accept`, health.
  - The Hangfire dashboard (`/hangfire`, all environments) needs the `PlatformOperator` policy: `shelter:operator=true` and `amr=mfa`.
- Platform permissions:
  - `platform.staff.read` (sensitive): list staff, with colleagues' emails.
  - `platform.staff.manage` (sensitive): change roles, suspend, reactivate. Sensitive so that no non-admin role can grant itself anything.
  - `audit.read` (sensitive): checked by `AuditReader` itself (`AuditReadDeniedException`).

## MFA

- M2-4, ADR 0018. Identity's TOTP authenticator (RFC 6238, 6 digits, 30 s) and 10 single-use recovery codes.
- Required for the `administrator` role of the active organization and for platform operators. `MembershipTenantResolver` sets `IMfaContext.EnrollmentRequired` when such a session did not sign in with MFA (`amr` is not `mfa`). The `MfaRequirement` in every tenant policy then answers 403, and `GET /api/platform/session` returns `mfaEnrollmentRequired: true`. Optional for everyone else.
- Endpoints, under `/api/platform/session`, antiforgery like every module route:
  - `POST /login/mfa` (`LoginPlatformSessionMfa`, anonymous): the second login step after `status: mfaRequired`, with `code` or `recoveryCode`. It needs Identity's 5-minute two-factor cookie from the password step; otherwise 401.
  - `POST /mfa/setup` (`SetupPlatformSessionMfa`): a new key (`sharedKey`, `otpauth://totp/Shelter:{email}?…`). 409 once enabled.
  - `POST /mfa/enable` (`EnablePlatformSessionMfa`): confirms a code. It returns the recovery codes (shown once) and re-issues the session cookie as `amr=mfa`.
  - `POST /mfa/disable` (`DisablePlatformSessionMfa`): needs a current code; the session goes back to `amr=pwd`.
  - Enrollment, removal and each reset of the key rotate the security stamp, so other sessions end.
- Codes:
  - A wrong code counts toward the lockout (5 → 15 min), including on enable and disable.
  - Recovery codes are stored as SHA-256 hashes (`ShelterUserStore`); Identity's default keeps them in plain text.
  - The authenticator key is stored in plain text in `user_token`, because checking a code needs it.
- `security_event` records `MfaEnabled`, `MfaDisabled` and `RecoveryCodeUsed`, plus `LoginSucceeded`/`LoginFailed`/`LockedOut` for the second step. The password step of an MFA login records nothing.
- Development bypass: `Auth:Mfa:DevelopmentBypass=true` (set in `appsettings.Development.json`) turns enforcement off **only** when the environment is Development, with a startup warning. It is ignored everywhere else (`MfaEnforcement.From`).

## Invitations

- M2-5, ADR 0018. Staff are onboarded by an email invitation; there is no self-sign-up.
- `platform.staff_invitation` (tenant-owned, forced RLS): email (personal), role keys, language (`fr`/`en`), inviter, created, expires (7 days), accepted/revoked. At most one open invitation per email per organization (filtered unique index). Accepted and revoked rows stay for history.
- The secret is 32 random bytes, sent base64url in the link's fragment (`{App:PublicBaseUrl}/accept-invitation#token=…`), stored only as its SHA-256.
- `platform.invitation_token` is **intentionally global**: `token_hash` → `organization_id`, `invitation_id`, `expires_at`, no personal data. It lets an anonymous request find the organization before any tenant is set. Its rows are deleted on accept, revoke and resend.
- Endpoints, under `/api/platform/invitations`:
  - `GET` (`ListPlatformInvitations`, `platform.staff.read`): open invitations, `pending` or `expired`.
  - `POST` (`CreatePlatformInvitation`, `platform.staff.manage`): 409 when the email already has a membership or a pending invitation. An expired one is revoked and replaced. The email is sent in the same transaction: a failed send creates nothing.
  - `POST /{id}/resend` and `POST /{id}/revoke` (`platform.staff.manage`): resend renews the 7 days and issues a new secret (the old link dies).
  - `POST /lookup` (anonymous): organization name, email, `accountExists`, for the accept page.
  - `POST /accept` (anonymous): a new email sets a name and password (the account is created with the email confirmed). An email that already has an account needs that account signed in (403 otherwise); an email match alone grants nothing. 409 if already a member.
  - An unknown, expired, revoked or used secret is always 404.
- `InvitationRedemption` reads the global token, then works in a **new DI scope** whose tenant is the token's organization, in one unit of work, with the invitation locked `FOR UPDATE`. The request's own scope may belong to another organization.

## Password reset

- M2-5. Identity's reset tokens (data protection, bound to the security stamp, 2 hours).
- `POST /api/platform/session/password/forgot` (anonymous): always `202` with no body. A link (`{App:PublicBaseUrl}/reset-password#user={id}&token={token}`) is emailed only when the account exists. A failed send is logged by exception type only.
- `POST /api/platform/session/password/reset` (anonymous): unknown account, wrong, expired or used token → the same 400. Password policy errors → validation problem. Success rotates the security stamp (other sessions end, the link is spent) and records `PasswordChanged`.

## Email

- `IEmailSender` (`BuildingBlocks/Communications/EmailSender.cs`): minimal SMTP (`System.Net.Mail`), to be replaced by the Communications building block. Configuration section `Email` (`From`, `FromName`, `Host`, `Port`, `EnableSsl`, `UserName`, `Password`). Development points at Mailpit (`127.0.0.1:1025`, UI on `http://localhost:8025`).
- `PlatformEmails` builds the invitation and reset emails in `fr` or `en`: the account's preferred language, otherwise the invitation's. `PublicLinks` builds links from `App:PublicBaseUrl`.
- Tests swap in `CapturingEmailSender` (`ShelterApiFactory.Emails`).

## Admin app (M2-6)

- Screens in `apps/admin/src/features/platform/routes`. Routes are English and match the emailed links (`PublicLinks`): `/login`, `/login/mfa`, `/forgot-password`, `/reset-password`, `/accept-invitation` (public); `/organizations`, `/mfa/enroll` (signed in); `/platform/staff`, `/account/security` (inside the shell).
- Guards: `RequireSession` (401 → `/login`, remembering the page), then `RequireOrganization` (no active organization → picker; `mfaEnrollmentRequired` → forced enrollment), then `RequirePermission` per page. Navigation hides modules without their read permission; placeholder modules with no permission yet stay listed. All of this is UX only: the server enforces.
- The picker chooses automatically when there is exactly one membership. The staff page shows actions only with `platform.staff.manage`; a 409 from the last-administrator guard gets its own message.
- MFA enrollment shows the setup key and the `otpauth://` link (no QR code: it would need a new dependency) and the recovery codes once. There is no screen to turn MFA off yet (`DisablePlatformSessionMfa` exists).
- Tests: `apps/admin/src/app/router.test.tsx` (guards, forced enrollment, permission gating, 401 mid-session), `src/lib/auth/auth.test.ts`, the fetcher CSRF tests, and `tests/e2e/auth.spec.ts` (login → organization → animals, forced enrollment) in both locales.

## Key files

- `Domain/Organization.cs`, `Domain/TenantSetting.cs`, `Persistence/PlatformModelContributor.cs`, `Provisioning/OrganizationProvisioner.cs`.
- `Features/Ping/PingEndpoint.cs`: `GET /api/platform/ping` (`GetPlatformPing`).
- `Authorization/PlatformPermissions.cs`; the shared model is in `BuildingBlocks/Authorization/Permissions.cs` and `EndpointAuthorizationExtensions.cs`.
- `Features/Staff/StaffEndpoints.cs`, under `/api/platform/staff`:
  - `GET` (`ListPlatformStaff`)
  - `PUT /{membershipId}/roles` (`ChangePlatformStaffRoles`)
  - `POST /{membershipId}/suspend` (`SuspendPlatformStaff`) and `POST /{membershipId}/reactivate` (`ReactivatePlatformStaff`)
- `Features/Invitations/InvitationEndpoints.cs`, `InvitationRedemption.cs`, `Domain/StaffInvitation.cs`, `Communications/PlatformEmails.cs`.
- `Features/Session/PasswordEndpoints.cs`.
- `Features/Session/MfaEndpoints.cs`, `Identity/ShelterUserStore.cs`; the shared enforcement is in `BuildingBlocks/Authorization/Mfa.cs`.
- Migrations: `backend/Shelter.Migrations` (one assembly, ADR 0005).
- Test harness: `backend/Tests/Shelter.Testing` (`PostgresDatabase`, `TenantHarness.AssertIsolatedAsync`).

## Open questions / TODO

- MFA follow-ups:
  - The authenticator key could be encrypted at rest.
  - A used TOTP code can be replayed within its validity window (Identity does not track used steps).
- Invitation and reset follow-ups:
  - Expired `invitation_token` rows are not purged (only accept, revoke and resend delete them).
  - No rate limiting on the anonymous endpoints (forgot-password can be used to spam an address).
  - Forgot-password takes longer when the account exists (a timing side channel).
  - `lookup` tells the token holder whether the email has an account.
  - A reset does not clear a lockout.
- Memberships created before M2-3 have no roles, and so no permissions. The development seed gives the demo account `administrator` again.
- Known gaps (from the reviewer):
  - `AuditRecord.Metadata` is not classified, so callers must keep personal data out of it.
  - `EntityId` is assumed to be non-personal.
  - Nothing yet stops code from logging decrypted `AuditEntry` values; treat them as personal.
- `appsettings.Development.json` holds a **dev-only** audit master key. Production needs a real `IKeyProvider`; the AWS KMS provider is a stub.
- The Hangfire tests run serially because they share the server's storage.
- Local databases created before M1 need `docker compose … down -v` once, for the `shelter_platform_admin` role.
