# M0 — Repository Foundation

Goal: an empty but fully wired repo where `docker compose up`, `pnpm dev`, and CI all work, and the conventions in `CLAUDE.md` are real.

Order matters: M0-1 → M0-2 → (M0-3, M0-4 in parallel) → M0-5 → M0-6 → M0-7. M0-8 can be written anytime during M0 but **ADR #5 must be accepted before M1 starts**.

---

## M0-1 Monorepo skeleton

**Docs:** architecture §26

**Acceptance criteria**
- [ ] Folder structure matches architecture §26 (`apps/admin`, `packages/{ui,api-client,config,adoption-widget}`, `backend/{Shelter.Host,Modules,BuildingBlocks,ArchitectureTests}`, `infrastructure`, `docs`, `scripts`).
- [ ] pnpm workspace + Turborepo configured; `pnpm install` works from root.
- [ ] .NET solution with central package management, `Directory.Build.props` (nullable enabled, warnings as errors, analyzers on, `LangVersion` latest).
- [ ] `.editorconfig`, Prettier, ESLint shared config in `packages/config`.
- [ ] `.gitignore` covers bin/obj/node_modules/.env*/generated output.
- [ ] Module folders exist as empty projects: Animals, People, Movements, Medical, Operations, Engagement, Municipal, Reporting, Platform.

---

## M0-2 Local infrastructure

**Docs:** architecture §28

**Acceptance criteria**
- [ ] `infrastructure/docker/compose.yml` with PostgreSQL (current major; `unaccent` and `pg_trgm` available), MinIO, Mailpit.
- [ ] Postgres init creates **two roles**: `shelter_migrator` (owns schema, runs migrations) and `shelter_app` (runtime; no `BYPASSRLS`, not table owner).
- [ ] `scripts/db-migrate-local.sh` applies migrations to the local DB only (refuses non-localhost connection strings).
- [ ] `.env.example` documents all local settings; no real secrets committed.
- [ ] README "Getting started" section: clone → compose up → migrate → run, in under 10 steps.

---

## M0-3 Backend host

**Docs:** architecture §5, §24

**Acceptance criteria**
- [ ] `Shelter.Host` boots with health checks (`/health/live`, `/health/ready` incl. DB).
- [ ] ProblemDetails for all errors.
- [ ] Structured logging with correlation/request IDs; a log-scrubbing hook exists (even if the field list is empty for now).
- [ ] OpenAPI document generated at build/run time.
- [ ] Module registration pattern: each module exposes one registration entry point (services + endpoints); Host only calls those.
- [ ] One trivial endpoint (`GET /api/platform/ping`) with a test proving the pipeline works.

---

## M0-4 Admin app shell

**Docs:** architecture §4, `apps/admin/CLAUDE.md`

**Acceptance criteria**
- [ ] Vite + React + TS app with React Router, TanStack Query provider, Tailwind, shadcn/ui set up via `packages/ui`.
- [ ] react-i18next configured: `fr-CA` default, `en-CA` available, language switcher in the shell, namespace-per-module structure.
- [ ] ESLint rule (or equivalent) that flags hard-coded JSX string literals.
- [ ] Dev server proxies `/api` to the backend (same-origin for cookies).
- [ ] Layout shell: sidebar with placeholder module entries, header with language switcher.
- [ ] PWA manifest + service worker registered (no offline caching of API data).
- [ ] One Vitest test and one Playwright smoke test (loads shell in fr-CA and en-CA).

---

## M0-5 OpenAPI → Orval pipeline

**Docs:** architecture §4.2

**Acceptance criteria**
- [ ] `pnpm api:generate` exports the backend OpenAPI document and runs Orval into `packages/api-client/src/generated`.
- [ ] Orval generates TanStack Query hooks (and Zod schemas if enabled).
- [ ] Admin app calls `GET /api/platform/ping` through the generated hook.
- [ ] Generated folder is excluded from Claude reads (already in `.claude/settings.json`) and from lint.

---

## M0-6 Architecture tests

**Docs:** architecture §5.3

**Acceptance criteria**
- [ ] Test project enforcing: modules may reference only other modules' `Contracts`; `BuildingBlocks` may not reference modules; `Host` contains no domain types.
- [ ] A deliberately failing example (commented or in a test) proves the rule catches violations.
- [ ] Runs in `dotnet test`.

---

## M0-7 CI

**Docs:** architecture §29

**Acceptance criteria**
- [ ] GitHub Actions workflow on PR: backend build + test (Testcontainers Postgres works in CI), frontend lint + test + build, Playwright smoke, `pnpm api:generate` followed by a "no diff" check.
- [ ] Branch protection on `main` documented (require CI green).
- [ ] CI time under ~10 minutes.

---

## M0-8 Seed ADRs

**Docs:** `docs/adr/README.md` backlog

**Acceptance criteria**
- [ ] ADRs 1–14 from the backlog written (short; one page max each).
- [ ] **ADR #5 (DbContext strategy) accepted.** Recommended default to evaluate: a single application DbContext composed from module-owned configurations, schema-per-module, because §7.1 requires cross-module writes (movement + timeline) in one transaction and `SET LOCAL` is per transaction. Evaluate against per-module DbContexts sharing one connection/transaction.
- [ ] Index in `docs/adr/README.md` updated.

**Owner note:** Daniel decides ADR #5. Claude drafts options.
