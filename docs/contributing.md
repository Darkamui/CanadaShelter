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

Runs on every pull request and on pushes to `main`. Four parallel jobs on `ubuntu-latest`, each capped at 15 minutes (target: the whole run under ~10 minutes).

| Job        | What it checks                                                                                   |
| ---------- | ------------------------------------------------------------------------------------------------ |
| `backend`  | `dotnet build` (warnings are errors), `dotnet format --verify-no-changes`, `dotnet test` (unit, architecture, and integration tests; PostgreSQL via Testcontainers) |
| `frontend` | Prettier, ESLint + `tsc`, Vitest, production build                                               |
| `e2e`      | Playwright shell smoke in `fr-CA` and `en-CA` against `vite preview`; report uploaded on failure  |
| `api-sync` | `pnpm api:generate`, then fails if `openapi.json` or `packages/api-client/src/generated` changed |

Third-party actions are pinned to commit SHAs (version in a comment). Update them deliberately, not with floating tags.

## Branch protection on `main`

Required settings (a repository admin applies them once; they are not managed by code yet):

- Require a pull request before merging (0 approvals while the team is one person).
- Require status checks to pass, and branches to be up to date: `backend`, `frontend`, `e2e`, `api-sync`.
- Include administrators.
- Block force pushes and branch deletion.

With the GitHub CLI (as an admin):

```sh
gh api -X PUT repos/Darkamui/CanadaShelter/branches/main/protection --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "checks": [
      { "context": "backend" },
      { "context": "frontend" },
      { "context": "e2e" },
      { "context": "api-sync" }
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": { "required_approving_review_count": 0 },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF
```

The check names are the job names in `ci.yml`. If a job is renamed, update this list and the protection rule together. A check only becomes selectable after it has run at least once, so push the first PR before applying the rule.
