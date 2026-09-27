# 0009. One frontend runtime (Vite); social previews via backend share endpoint

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

V1 needs the staff app, public adoption listings and forms, portal routes, and an embeddable widget (§4.1). Rescues rely on Facebook/Instagram sharing, and crawlers can't read per-animal meta tags from a client-rendered SPA (§18A.3).

## Decision

- **One runtime:** a single React + TypeScript + **Vite** SPA (`apps/admin`), installable as a PWA. It hosts staff routes and, in V1, the public and portal routes.
- **Widget:** the embeddable widget is a separate package (`packages/adoption-widget`) built with the same toolchain.
- **Social previews:** the backend serves a lightweight share endpoint (e.g. `/s/{tenantSlug}/{animalPublicId}`). It returns minimal server-rendered HTML with localized Open Graph/Twitter tags, then redirects human visitors to the SPA or the shelter's page.
- **Next.js:** no Next.js (or any second frontend runtime) in V1.

## Alternatives considered

- **Next.js for public pages + Vite for staff**: two runtimes, two deployment targets and a Node server in production, just to solve previews, which one backend endpoint already solves.
- **Next.js for everything**: SSR complexity and server hosting for a staff app that doesn't need SEO.
- **Prerendering service**: another moving part, and its cache would serve stale animal data.

## Consequences

- Positive: one build, one deploy artifact (static files) and one set of conventions.
- Negative / accepted trade-offs:
  - Public listing pages get limited SEO.
  - The share endpoint duplicates a little presentation logic on the server.
- Follow-ups:
  - The share endpoint is required from the pilot (product-spec §41.2).
  - Revisit with a new ADR if SEO, CMS or public-traffic requirements justify a second runtime.
