# 0007. ASP.NET Identity + same-site cookie auth; no OpenIddict in V1

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

V1 users are staff and external participants: fosters, volunteers, adopters and partners (§9.1, §9.3). Only our own SPA calls our own API; no third party needs tokens from us yet (§9.4). Browser tokens stored in JS-readable storage are an XSS exfiltration risk.

## Decision

- **Accounts:** ASP.NET Core Identity manages accounts, password hashing, lockout and MFA.
- **Session cookie:** authentication uses a **secure, HttpOnly, SameSite** cookie, with CSRF protection on state-changing requests.
- **Serving model:** the SPA and API are served same-origin, or at least same-site. Locally, the Vite proxy forwards `/api` to the Host, and the generated client uses `credentials: 'same-origin'`.
- **No tokens in storage:** no auth tokens in `localStorage`/`sessionStorage`. `localStorage` holds UI preferences only, such as the locale.
- **Staff vs external:** separate membership types. A matching email never grants staff access.

## Alternatives considered

- **OpenIddict / Duende as our own authorization server**: only needed once we issue OAuth/OIDC credentials to third parties. It's significant setup and ongoing security surface, and adding one needs an ADR (hard rule 8).
- **JWT bearer tokens in the SPA**: token storage and refresh complexity, XSS exposure, and revocation is harder.
- **External IdP (Auth0, Entra, Cognito)**: another processor of personal data to review under Law 25 (§16.2), plus residency questions and per-user cost.

## Consequences

- Positive:
  - Minimal attack surface: no tokens reachable from JavaScript.
  - Mature, first-party library.
- Negative / accepted trade-offs:
  - Deployment must keep the SPA and API same-site.
  - The embeddable widget and any future public API must be anonymous or use separate credentials (§18A).
  - Municipal SSO will need an external OIDC/SAML integration later.
- Follow-ups:
  - M2 implements Identity, cookie settings, CSRF, MFA and a default-deny fallback policy.
  - Machine integrations get scoped API credentials when they're needed.
