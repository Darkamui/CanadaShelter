# apps/admin/CLAUDE.md

React + TypeScript + Vite PWA. React Router, TanStack Query/Table, React Hook Form + Zod, Tailwind + shadcn/ui, react-i18next, Vitest, Playwright.

## Layout

```text
src/
  app/                 Router, providers, layout shell
  features/<module>/   Mirrors backend modules (animals, people, movements, ...)
    routes/            Route components
    components/        Feature-specific components
    i18n/              fr-CA.json, en-CA.json (namespace = module)
  public/              Public routes (listing, application form) — no staff auth
  lib/                 Formatting, i18n setup, auth helpers
```

Shared primitives live in `packages/ui`. Generated API hooks come from `packages/api-client`.

## Conventions

- **Server state:** only via Orval-generated TanStack Query hooks. No hand-written `fetch` to the API.
- **After a backend contract change:** run `pnpm api:generate`; never edit generated files.
- **Forms (ADR 0019):** React Hook Form + Zod through `useZodForm` (`lib/forms`), with `FormField`/`FormAlert` for display.
  - Schema messages are i18n keys with their namespace (`shell:forms.required`), never text. Shared builders: `requiredText`, `emailAddress` (`lib/forms/schemas.ts`). Never trim a password.
  - A 400 ValidationProblem goes on the fields with `applyValidationErrors` (server messages are English only, so map each field to a catalog key); anything else shows in a `FormAlert`.
  - Reuse Orval-generated Zod schemas (`@shelter/api-client/zod/*`) when the form matches the request body, overriding their messages with keys.
- **i18n:** no hard-coded user-facing strings (lint enforced). Keys namespaced per module. Write `fr-CA` first, then `en-CA`. Default locale `fr-CA`.
- **i18n catalogs:** shell strings in `src/app/i18n/<locale>.json` (namespace `shell`, the default); module strings in `src/features/<module>/i18n/<locale>.json` are picked up automatically by `src/lib/i18n`. Uncertain French terms: list their keys in a top-level `"_frReview": [...]` array in `fr-CA.json`. `catalogs.test.ts` fails if fr-CA and en-CA keys differ.
- **PWA:** the service worker precaches the app shell only. Never add `runtimeCaching` for `/api` (tenant data, permissions).
- **Reference data:** the API returns both `fr`/`en` labels; pick by UI locale via the shared helper.
- **Dates/numbers/currency:** `Intl` helpers in `lib/format`, organization timezone, CAD.
- **Auth:** cookie-based (same-site). Never store tokens in `localStorage`/`sessionStorage`.
  - `lib/auth/session.ts` (`useSession`, `hasPermission`, `resetSession`) and the route guards in `app/auth/` (`RequireSession` → `RequireOrganization` → `RequirePermission`). A 401 from any API call rechecks the session (`onUnauthorized` in `app/providers.tsx`), and the guard sends the user to `/login`.
  - The fetcher (`packages/api-client/src/http/fetcher.ts`) echoes the `XSRF-TOKEN` cookie as `X-XSRF-TOKEN` on unsafe requests, fetching it first if missing.
  - The return path and notices travel in router state only (`AuthState`). Emailed secrets arrive in the URL fragment and are stripped on read (`useLinkFragment`).
  - After sign-in, an organization switch or an MFA change, call `resetSession(queryClient)`: cached data belonged to the previous user or organization.
- **Permissions:** hide actions the user lacks, but the backend is the enforcement point.
- **Accessibility:** public routes target WCAG 2.2 AA. Use shadcn/Radix primitives, labelled inputs, visible focus, accessible errors.
- **Tables:** TanStack Table; server-side pagination/filtering for anything that can exceed ~200 rows.

## Tests

- Vitest for logic/components.
- Playwright E2E for critical workflows, run in **both** `fr-CA` and `en-CA`. Specs live in `tests/e2e/`, run against `vite preview`, and mock `/api/**` with `page.route` (service workers are blocked so mocks apply).
  - `tests/e2e/api.ts` (`mockBaseApi`) holds the session and catch-all mocks. A workflow that writes and reads back uses a small in-memory fake registered after it (e.g. `fakeCoreRecords.ts`), which calls `route.fallback()` for paths it does not handle. Playwright matches the most recently added route first.
  - Run from the repo root (`pnpm test:e2e`).
