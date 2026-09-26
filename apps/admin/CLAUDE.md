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
- **Forms:** React Hook Form + Zod. Reuse Orval-generated Zod schemas when available.
- **i18n:** no hard-coded user-facing strings (lint enforced). Keys namespaced per module. Write `fr-CA` first, then `en-CA`. Default locale `fr-CA`.
- **i18n catalogs:** shell strings in `src/app/i18n/<locale>.json` (namespace `shell`, the default); module strings in `src/features/<module>/i18n/<locale>.json` are picked up automatically by `src/lib/i18n`. Uncertain French terms: list their keys in a top-level `"_frReview": [...]` array in `fr-CA.json`. `catalogs.test.ts` fails if fr-CA and en-CA keys differ.
- **PWA:** the service worker precaches the app shell only. Never add `runtimeCaching` for `/api` (tenant data, permissions).
- **Reference data:** the API returns both `fr`/`en` labels; pick by UI locale via the shared helper.
- **Dates/numbers/currency:** `Intl` helpers in `lib/format`, organization timezone, CAD.
- **Auth:** cookie-based (same-site). Never store tokens in `localStorage`/`sessionStorage`.
- **Permissions:** hide actions the user lacks, but the backend is the enforcement point.
- **Accessibility:** public routes target WCAG 2.2 AA. Use shadcn/Radix primitives, labelled inputs, visible focus, accessible errors.
- **Tables:** TanStack Table; server-side pagination/filtering for anything that can exceed ~200 rows.

## Tests

- Vitest for logic/components.
- Playwright E2E for critical workflows, run in **both** `fr-CA` and `en-CA`. Specs live in `tests/e2e/`, run against `vite preview`, and mock `/api/**` with `page.route` (service workers are blocked so mocks apply).
