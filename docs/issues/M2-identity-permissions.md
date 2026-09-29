# M2 — Identity, Memberships and Permissions

Goal: staff sign in with ASP.NET Identity (secure cookie, CSRF, MFA). The tenant comes from a verified staff membership, not a header. Every endpoint is **denied by default** and declares a permission. Invariants are **proven by tests**, as in M1.

**Owner note:** this milestone is security-critical. Daniel reviews every migration and every line in `BuildingBlocks/Authorization`, `BuildingBlocks/Tenancy` and the authentication composition. Run the `tenancy-privacy-reviewer` agent on every M2 PR.

Prerequisites:

- M1 merged, or branch from `m1/platform-foundations` until it is.
- An ADR for the identity and authorization model (global accounts, membership self-read policy, active organization claim, code-defined roles, CSRF, MFA bypass, invitation tokens) accepted before M2-1.
- New dependency approved: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` only. No QR-code library: MFA enrollment shows the setup key and the `otpauth://` link.

Order: M2-1 → M2-2 → M2-3 → (M2-4, M2-5 in parallel) → M2-6.

---

## M2-1 Accounts and cookie authentication

**Docs:** architecture §9.1, ADR 0007

**Acceptance criteria**

- [x] `UserAccount` (Platform, internal) on ASP.NET Identity, stored in the composed `ShelterDbContext` (`UserOnlyStore`, no Identity roles).
  - It is a **global** table: one account, many organizations.
  - Every column is classified.
  - The table is excluded from the tenant audit, with the reason recorded in the ADR.
- [x] Secure session cookie:
  - `__Host-` name, HttpOnly, Secure, SameSite=Strict, sliding expiry.
  - Security stamp validation: a password or MFA change ends other sessions.
- [x] API requests get **401/403 ProblemDetails**, never login redirects.
- [x] Password policy (minimum length 12, no composition rules) and lockout (5 failures → 15 min).
- [x] Login responses never reveal whether an account exists.
- [x] Endpoints: login, logout, and current session (user, memberships, active organization, permissions).
- [x] CSRF protection on every state-changing request, including login, using built-in antiforgery (double-submit token and header).
- [x] Append-only `platform.security_event` (user id, event type, time, correlation id).
  - Covers login succeeded/failed/locked out, password changed, and MFA changes.
  - No email or IP address.
- [x] Development-only seed: a demo organization and an administrator. It is ignored outside Development.
- [x] Tests: login/logout/session; same response for a wrong password and an unknown email; lockout; unsafe request without a CSRF token → rejected; cookie flags; 401 is ProblemDetails.

---

## M2-2 Staff memberships and tenant resolution

**Docs:** architecture §8.1, §9.3

**Acceptance criteria**

- [x] Tenant-owned `StaffMembership` (user, status active/suspended):
  - Unique `(TenantId, UserId)` and RLS through the helper.
  - A **self-read policy**: users can list their own memberships before choosing an organization. The unit of work sets `app.user_id` with `SET LOCAL`.
- [x] The tenant comes from the active organization in the session.
  - Every request re-checks it against an **active** membership.
  - A suspended or removed membership → no tenant data (403).
- [x] A user with one membership has it selected automatically. Switching organizations only chooses among the caller's own memberships, validated server-side.
- [x] The dev `X-Tenant-Id` header resolver is removed. Tests authenticate through a test-only authentication handler, plus real cookie-login tests.
- [x] Staff and external participants remain separate authorization classes. An email address matching a staff member's **never** grants staff access.
- [x] Tests: a member of A cannot select or read B; suspended → 403; no membership → no tenant data; the self-read policy shows only the caller's rows; the pooled-connection test covers `app.user_id`.

---

## M2-3 Permissions, roles and default deny

**Docs:** architecture §9.2, §33

**Acceptance criteria**

- [x] Permission catalog: each module declares its permissions (read/write, sensitive) in its `Authorization/` folder.
  - Initial set: `platform.staff.read`, `platform.staff.manage`, `audit.read`, `animal.read`, `movement.read`.
- [x] Roles are collections of permissions. The system roles `administrator` (all), `staff` (non-sensitive) and `read_only` (non-sensitive reads) are assigned per membership. Custom roles are a follow-up.
- [x] Endpoints declare `.RequirePermission(...)`. An authorization **fallback policy** denies everything else: authenticated user and active tenant required.
- [x] Anonymous access is an explicit allowlist (ping, login, antiforgery, password reset, invitation accept). _Password reset and invitation accept join the allowlist with their endpoints in M2-5._ A test enumerates every endpoint and fails on any endpoint that neither declares a permission nor is allowlisted.
- [x] M1 follow-ups:
  - The species and intake-reason lists require `animal.read` / `movement.read`.
  - `IAuditReader` requires `audit.read`.
  - The Hangfire dashboard is limited to platform operators with MFA (never granted through the API).
- [x] Staff management: list members, change roles, suspend/reactivate.
  - Requires `platform.staff.manage`.
  - Changes are audited (permission changes).
  - The last active administrator cannot be removed or demoted.
- [x] Tests: permission denied (403) for every endpoint; default deny for an endpoint with no permission; role change audited; last-administrator guard; cross-tenant member management denied.

---

## M2-4 Multi-factor authentication (TOTP)

**Docs:** architecture §9.1, ADR 0007

**Acceptance criteria**

- [x] TOTP enrollment (setup key and `otpauth://` link, confirmed by a code), 10 single-use recovery codes, a two-step login, and disabling that requires a code.
- [x] MFA is **required** for administrators and platform operators. Until they enroll, their session is limited to enrollment. It is optional for other staff.
- [x] **Local development bypass:** a configuration flag skips enforcement **only in the Development environment**. It logs a startup warning and has no effect in any other environment.
- [x] MFA enrollment and removal are recorded in `security_event`.
- [x] Tests: an administrator without MFA is blocked outside enrollment; full access after enrollment; a recovery code works once; the bypass is honored only in Development.

---

## M2-5 Staff invitations and password reset

**Docs:** architecture §14.4, §16.4

**Acceptance criteria**

- [x] Minimal `IEmailSender` over SMTP (Mailpit locally). It is replaced by the Communications building block later.
- [x] Tenant-owned `StaffInvitation`:
  - Stores the email (**personal**, classified), roles and language.
  - Expires after 7 days; can be revoked.
  - The token is 32 random bytes, stored only as a hash, single use.
- [x] An anonymous accept finds the organization from the token alone. A global token lookup table holds no personal data.
- [x] Accepting:
  - A new account sets a name and password.
  - An existing account must be signed in as that account.
  - An email match alone grants nothing.
- [x] Forgot/reset password: always the same response (no account enumeration).
- [x] Invitation and reset emails in fr-CA or en-CA, following the recipient's language. No personal data in logs.
- [x] Tests: accepting creates the membership with its roles; expired, revoked or reused token rejected; a token cannot reach another tenant; reset flow; emails captured by a fake sender.

---

## M2-6 Admin app authentication

**Docs:** architecture §4.1, §4.2, §9.1

**Acceptance criteria**

- [x] The API client sends the CSRF header on unsafe requests. A 401 sends the user to the login page.
- [x] Screens:
  - login and MFA challenge;
  - MFA enrollment (setup key, otpauth link, recovery codes shown once) and forced enrollment;
  - organization picker;
  - forgot/reset password and accept invitation;
  - staff list (invite, change roles, suspend).
- [x] Routes are guarded by session. Navigation hides modules the user cannot access; the server still enforces.
- [x] All strings in fr-CA and en-CA (Québec French; `TODO(fr-review)` where unsure).
- [x] Tests: Vitest (guard, CSRF header, MFA and permission gating); Playwright (login → organization → animals, forced enrollment).

---

## M2-7 Admin form stack (follow-up, added before M3)

**Docs:** ADR 0019

**Acceptance criteria**

- [x] The M2 forms use React Hook Form + Zod through `useZodForm`. Error messages are catalog keys, translated at render time.
- [x] Server validation errors show on their field (`applyValidationErrors`).
- [x] Tests: schemas and server-error mapping (Vitest); the reset, forgot and invite forms block invalid input.

---

## M2-8 Auth hardening (follow-up, added before M3)

**Docs:** ADR 0020, ADR 0010 addendum

**Acceptance criteria**

- [x] The anonymous endpoints that take a password, code or secret are rate limited per client IP, with a `429` problem and `Retry-After`. The admin app says "too many attempts".
- [x] Forgot-password sends its email from a background job, enqueued for known and unknown emails alike (closes the timing side channel). Job arguments are IDs only.
- [x] A recurring global job purges expired `invitation_token` rows hourly.
- [x] Tests: 429 over the limit and a guard that every other anonymous endpoint is limited (`RateLimitingTests`); the purge (`GlobalJobTests`); reset through the job (`PasswordResetTests`).

---

## M2 exit criteria

- [x] Every endpoint declares a permission or is on the anonymous allowlist (coverage test green in CI on PR #3).
- [x] No `TODO(M2)` authentication or permission markers remain.
- [x] `tenancy-privacy-reviewer` reports no Critical/High findings on M2 code (full M2 range reviewed 2026-09-27: none; deferred items listed in `docs/modules/platform.md`).
- [x] `docs/modules/platform.md` describes identity, memberships, permissions and MFA.
- [x] Daniel has personally reviewed all M2 migrations (PR #3, merged).

Follow-ups (not M2): custom roles, a shared DataProtection key ring before multi-instance deployment, external portal memberships (architecture §34 step 16), SSO (§9.4).

Next: M3 (People, Animals, Locations, Movements + timeline).
