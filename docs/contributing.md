# Contributing

## Workflow

- One branch per issue (`m1/rls-set-local`), conventional commits, pull request into `main`.
- Never push directly to `main`; never force-push a shared branch.
- Before opening a PR, run what CI runs (see `CLAUDE.md` → "Done means green"):

```sh
dotnet build backend && dotnet test backend
pnpm format:check && pnpm lint && pnpm test && pnpm build
pnpm test:e2e
pnpm api:generate && git status --porcelain   # must print nothing
```

## CI (`.github/workflows/ci.yml`)

Runs on every pull request and on pushes to `main`. Seven parallel jobs on `ubuntu-latest`, each capped at 15 minutes (target: the whole run under ~10 minutes).

| Job        | What it checks                                                                                   |
| ---------- | ------------------------------------------------------------------------------------------------ |
| `backend`  | `dotnet build` (warnings are errors), `dotnet format --verify-no-changes`, `dotnet test` (unit, architecture, and integration tests; PostgreSQL via Testcontainers) |
| `frontend` | Prettier, ESLint + `tsc`, Vitest, production build                                               |
| `e2e`      | Playwright shell smoke in `fr-CA` and `en-CA` against `vite preview`; report uploaded on failure  |
| `api-sync` | `pnpm api:generate`, then fails if `openapi.json` or `packages/api-client/src/generated` changed |
| `migrations` | PRs only: fails if a migration that exists on the base branch is modified, renamed or deleted (hard rule 10) |
| `dependency-review` | PRs only: fails on added dependencies with a known vulnerability of moderate severity or higher |
| `workflows` | `actionlint` (workflow syntax) and `zizmor` (workflow security, medium severity and up) |

Third-party actions are pinned to commit SHAs (version in a comment), and checkouts use `persist-credentials: false`. Dependabot (`.github/dependabot.yml`) opens weekly update PRs for actions, NuGet and npm, and monthly for the Compose images; review them like any other PR. Dependabot only updates existing dependencies. A new one still needs approval (CLAUDE.md hard rule 9).

`.github/workflows/codeql.yml` runs CodeQL (C#, JS/TS, Actions; `security-extended`) on PRs, on `main`, and weekly. It is not a required check; review alerts in the Security tab.

## Branch protection on `main`

Managed as code in `.github/branch-protection.json`:

- Require a pull request before merging (0 approvals while the team is one person).
- Require status checks to pass, and branches to be up to date: `backend`, `frontend`, `e2e`, `api-sync`, `migrations`, `dependency-review`, `workflows`.
- Include administrators.
- Block force pushes and branch deletion.

Apply or re-apply it with the GitHub CLI (as an admin):

```sh
gh api -X PUT repos/Darkamui/CanadaShelter/branches/main/protection --input .github/branch-protection.json
```

The check names are the job names in `ci.yml`. If a job is renamed, update `.github/branch-protection.json` and re-apply it. A check only becomes selectable after it has run at least once, so push the first PR before applying the rule.
