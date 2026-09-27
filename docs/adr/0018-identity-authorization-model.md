# 0018. Identity and authorization model

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

ADR 0007 chose ASP.NET Identity with a same-site cookie. M2 has to connect that to tenancy (ADR 0003, 0004) and to permission-based, default-deny authorization (architecture §9.2). Three facts constrain the design:

- One person can be staff at several organizations.
- Before choosing an organization, a user must see which organizations they belong to, but RLS hides every tenant row when no tenant is set.
- The tenant must never come from the client (hard rule 2).

## Decision

- **Global accounts.**
  - `platform.user_account` (plus `user_token`, `user_claim`, `user_login`) is one global table: one account, many memberships. It is Identity's `UserOnlyStore` over the composed `ShelterDbContext`; there are no Identity roles.
  - Every column is classified (`HasAuditSubject(u => u.Id)`). The tenant audit covers only `ITenantOwned` rows, so accounts are **not** in `audit.audit_event`. Security-relevant account activity goes to a global, append-only `platform.security_event` instead: user id, event type, time and correlation id, with no email or IP.
- **Staff memberships** (`platform.staff_membership`) are tenant-owned, under the standard RLS policies, **plus one self-read policy**: `FOR SELECT USING (user_id = platform.current_user_id())`.
  - The transaction interceptor sets `SET LOCAL app.user_id` next to `app.tenant_id`, from a scoped `UserContext`.
  - A membership's roles are a `role_keys text[]` column on the membership row, not a separate table. The self-read policy then covers roles as well, one row read resolves both tenant and permissions, and a role change is an audited update of the membership.
- **Active organization.**
  - The chosen organization is a claim in the data-protected auth cookie.
  - `MembershipTenantResolver` re-checks it on **every request** against an **active** membership of the signed-in user, in one self-read transaction, before setting `TenantContext`. A missing, suspended or removed membership means no tenant, and the fallback policy then returns 403.
  - Switching organizations (`POST …/session/organization/{id}`) only re-issues the cookie for one of the caller's own active memberships.
  - The client can name an organization but never grant itself one, so hard rule 2 holds.
- **Permissions.**
  - Each module declares `PermissionDefinition`s (name, read/write, sensitive) in its `Authorization/` folder. The catalog is composed in DI.
  - Roles are code-defined over the catalog:
    - `administrator` = every permission, including future ones.
    - `staff` = every non-sensitive permission.
    - `read_only` = every non-sensitive read.
  - Endpoints declare `.RequirePermission(...)`, or `.RequireSession()` for the few that need a signed-in user but no organization (session, logout, organization switch, MFA enrollment).
  - The authorization **fallback policy** requires an authenticated user, an active tenant and satisfied MFA, so default deny is structural.
  - Anonymous endpoints are an explicit allowlist, enforced by a test that enumerates every endpoint.
- **CSRF: antiforgery double-submit.**
  - `GET …/session/antiforgery` sets a readable `XSRF-TOKEN` cookie.
  - An endpoint filter on every module route validates the `X-XSRF-TOKEN` header on unsafe methods, login included.
  - The session cookie is `__Host-`, HttpOnly, Secure and SameSite=Strict. API calls get 401/403 ProblemDetails, never redirects.
- **MFA.**
  - Identity's TOTP authenticator with hashed recovery codes.
  - Required for the `administrator` role and for platform operators (`user_account.is_platform_operator`, set only by SQL or provisioning). Until they enroll, their session reaches only the `.RequireSession()` endpoints.
  - `Auth:Mfa:DevelopmentBypass` skips enforcement **only when the environment is Development** and logs a warning at startup. Everywhere else it is ignored.
- **Invitations.**
  - The invitation (tenant-owned, email classified personal) is found through a global `platform.invitation_token` table (SHA-256 token hash → tenant id, invitation id; no personal data). An anonymous accept therefore resolves the tenant from the secret alone.
  - An email match alone never grants anything.

## Alternatives considered

- **Per-tenant accounts**: this duplicates credentials and MFA for people who work at several organizations, and makes "log in, then choose" impossible.
- **Listing memberships through the platform-admin role**: this would widen that role's use into every login. The narrow self-read policy only exposes the caller's own rows.
- **Active organization in a request header**: this is the M1 dev header, trusted from the client. The cookie claim is server-issued, and every request re-validates it.
- **`staff_membership_role` table**: this would need a second self-read policy (a join) or a second transaction per request, for no gain until custom roles exist.
- **Database-defined roles now**: custom roles are a follow-up. Code-defined roles keep M2 reviewable.
- **SameSite=Strict alone as CSRF defence**: it doesn't cover same-site subdomains or older clients. The token costs little.

## Consequences

- Positive:
  - Tenant resolution, permission checks and MFA enforcement are centralized and default-deny.
  - The client never supplies an authoritative tenant.
- Negative / accepted trade-offs:
  - One extra transaction per authenticated request (the membership re-check), on its own before the endpoint's unit of work.
  - Self-read lookups filter on `user_id` alone, while every index leads with `tenant_id` (hard rule 1), so they scan the table. Fine at pilot scale; revisit (for example a partial or per-tenant strategy that keeps the rule) if memberships grow large.
  - Authenticated requests now always open a transaction (`app.user_id`), even with no organization chosen.
  - Account changes appear in `security_event`, not in the tenant audit trail.
  - The runtime role can read every account row. That is inherent to global accounts, and endpoints expose only the caller's own account.
- Follow-ups:
  - **Shared DataProtection key ring** before any multi-instance deployment. Cookies and antiforgery tokens are per-instance until then. This needs storage or a dependency, so it gets its own ADR.
  - Custom roles; external portal memberships (architecture §34 step 16); SSO (§9.4).
  - **Login timing.** Responses are identical, and an unknown email pays a dummy hash check and a security-event write. A known email still does one more write (the failed-attempt count), so timing can hint that an account exists. Revisit (padding to a fixed floor) if enumeration becomes a real threat.
