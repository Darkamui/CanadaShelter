# 0020. Rate limiting the anonymous auth endpoints

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

M2 exposes six anonymous endpoints that take a password, a code or a secret:

- `session/login` and `session/login/mfa`;
- `session/password/forgot` and `session/password/reset`;
- `invitations/lookup` and `invitations/accept`.

Identity's lockout protects one account against password guessing. Nothing slows down one client that tries many accounts, guesses invitation secrets, or uses forgot-password to flood an inbox. CLAUDE.md hard rule 8 forbids new infrastructure (Redis, gateways) without an ADR, and rule 9 forbids new packages without asking.

## Decision

- **Built-in limiter.** Use ASP.NET Core's rate limiting middleware (`AddRateLimiter` / `UseRateLimiter`). It is part of the shared framework: no new package, no new infrastructure. It is configured in `Shelter.Host/Composition/RateLimiting.cs`.
- **Opt-in policy.** One named policy, `RateLimitPolicies.Anonymous` (`BuildingBlocks/Authorization`). Endpoints opt in with `.RequireRateLimiting(RateLimitPolicies.Anonymous)`. Signed-in endpoints and anonymous endpoints that take nothing secret (ping, antiforgery, health) are not limited.
- **Window.** A fixed window per client IP, shared by all six endpoints. The defaults are 20 requests per minute, with no queue. Both are configurable: `RateLimiting:Anonymous:PermitLimit` and `RateLimiting:Anonymous:Window`.
- **Answer.** Over the limit, the answer is `429` with a problem body and `Retry-After`. The admin app shows "too many attempts" on the auth screens (`isRateLimited`).
- **Guard.** `RateLimitingTests` fails if a new anonymous endpoint is neither rate limited nor on its short unlimited list.

## Alternatives considered

- **Reverse proxy or WAF limits** (for example at the load balancer). These are fine as an extra layer, but the deployment does not exist yet, and the rule would live outside the code and its tests.
- **Distributed limiter (Redis).** It gives exact limits across instances, but it is new infrastructure for a single-instance pilot.
- **Per-account limits** (keyed by email). They would stop inbox flooding from many IPs, but the key comes from the request body, and they duplicate Identity's lockout for passwords.
- **A sliding window or token bucket.** Smoother, but a fixed window is simpler to reason about at these volumes.

## Consequences

- Positive:
  - Brute force and spraying from one address are slowed down.
  - No dependency and no infrastructure were added.
  - The rule is enforced by a test.
- Negative / accepted trade-offs:
  - Counters are in memory, per instance: N instances allow N times the limit, and a restart resets them.
  - The partition is the connection's IP. Behind a proxy, every client shares the proxy's IP (one window for everybody) until forwarded headers are configured. Staff behind one office NAT also share a window, hence the fairly high default.
  - A connection with no client IP falls into one shared `unknown` window, so one abuser there can block every such client.
  - Inbox flooding from many IPs is still possible.
  - Test requests have no client IP, so the test host raises the limit, and only `RateLimitingTests` lowers it.
- Follow-ups:
  - Configure `ForwardedHeaders` (with known proxies) when the hosting is set up.
  - Revisit a distributed limiter if the API runs on more than one instance.
