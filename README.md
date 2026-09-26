# Shelter Platform

Québec-first, bilingual (fr-CA / en-CA) shelter and rescue management SaaS. Multi-tenant ASP.NET Core modular monolith, PostgreSQL, and a React/Vite PWA.

- Product direction: [docs/product-spec.md](docs/product-spec.md)
- Architecture: [docs/architecture.md](docs/architecture.md)
- Decisions: [docs/adr/](docs/adr/README.md)
- Working conventions (humans and Claude): [CLAUDE.md](CLAUDE.md)

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 24 (with corepack), Docker Desktop, Git Bash on Windows (for `scripts/*.sh`).

1. Clone the repository and `cd` into it.
2. Enable pnpm through corepack: `corepack enable`.
   (Windows without admin rights: `corepack enable --install-directory "$APPDATA/npm" pnpm`.)
3. Install JavaScript dependencies: `pnpm install`.
4. Start local infrastructure (PostgreSQL, MinIO, Mailpit):
   `docker compose -f infrastructure/docker/compose.yml up -d`
5. Apply database migrations to the local database: `./scripts/db-migrate-local.sh`
6. Run the API (http://localhost:5080): `dotnet run --project backend/Shelter.Host`
7. In another terminal, run the admin app (http://localhost:5173): `pnpm dev`

Every setting has a working dev default. To override one, see [.env.example](.env.example).

### Local services

| Service             | Address                                       | Credentials (dev only)             |
| ------------------- | --------------------------------------------- | ---------------------------------- |
| PostgreSQL          | `127.0.0.1:55432`, database `shelter`         | `shelter_app` / `shelter_migrator` |
| MinIO API / console | http://localhost:9000 / http://localhost:9001 | `shelter` / `shelter_minio_dev`    |
| Mailpit (SMTP / UI) | `localhost:1025` / http://localhost:8025      | —                                  |

PostgreSQL has two application roles: `shelter_migrator` owns the schema and runs migrations; `shelter_app` is the runtime role (no `BYPASSRLS`, owns no tables) so row-level security is always enforced. To reset the database: `docker compose -f infrastructure/docker/compose.yml down -v`.

## Everyday commands

| Command                                        | What it does                                      |
| ---------------------------------------------- | ------------------------------------------------- |
| `dotnet build backend` / `dotnet test backend` | Build / test the backend (tests need Docker)      |
| `pnpm lint` / `pnpm test` / `pnpm build`       | Lint / test / build all JavaScript packages       |
| `pnpm test:e2e`                                | Playwright E2E for the admin app (fr-CA + en-CA)  |
| `pnpm format:check`                            | Check Prettier formatting                         |
| `./scripts/db-migrate-local.sh`                | Apply migrations — refuses any non-local database |

## Repository layout

```text
apps/admin/          React/Vite staff app (PWA)
packages/            ui (shadcn/ui), api-client (Orval-generated), config (lint/format/tsconfig), adoption-widget
backend/             .NET solution: Shelter.Host, Modules/*, BuildingBlocks, ArchitectureTests
infrastructure/      docker (local), deployment (OpenTofu, later)
scripts/             Developer scripts
docs/                Specs, ADRs, module docs, issues
```
