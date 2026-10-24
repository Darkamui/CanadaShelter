# 0023. Development seed route

- **Status:** Accepted
- **Date:** 2026-10-17

## Context

Testing and demos need realistic volumes of data across every module: locations, people, animals with identifiers, and custody histories. The startup `DevelopmentSeed` only creates one organization and one administrator. Entering data by hand does not scale. Writing SQL directly would bypass the domain rules: the custody summary, the timeline, the gapless animal numbers, and audit classification. It would also cross module boundaries (hard rule 7). The runtime role cannot delete (append-only ledgers; archiving replaces deleting, architecture §7.1, §17), so "wipe and reseed" cannot be done through the app.

## Decision

- **Route.** `POST /api/platform/dev/seed` (`SeedPlatformDevData`) runs every module's `IDevSeedContributor` (`BuildingBlocks/DevSeed`) in `Order`: 5 Platform, 10 Operations, 20 People, 30 Animals, 40 Movements.
  - **Module boundaries.** Each contributor writes only its own tables through its own domain code and validators. Movements go through `MovementRecorder`.
  - **Passing IDs.** Contributors pass IDs to each other through `DevSeedContext` records, never through another module's tables.
- **Gating.**
  - The route is mapped only in Development with `DevSeed:Enabled`.
  - The sensitive permission `platform.dev.seed` (administrators only) is declared only when `DevSeed:Enabled` is set.
  - The route is excluded from OpenAPI, so the generated client never sees it.
- **Body.** All fields are optional.
  - `scale` (`small`/`demo`/`load` = factor 1/10/100) or `factor` (1–200).
  - `seed`: makes a run reproducible.
  - `newOrganization` and `organizationName`: provision a new organization via `OrganizationProvisioner`, with the caller as its administrator.
- **Execution.**
  - Each run executes in its own scope and **one transaction**, as the target organization.
  - The run has administrator permissions and the caller as user and audit actor (source `dev-seed`).
  - It returns the organization ID, the factor, the seed, and counts per item.
- **Additive only.** Seeding again adds data and reuses the location tree.
- **Reset.** `scripts/dev-reset-data.sh` truncates every non-platform table with `tenant_id`, as `shelter_migrator`, inside the local Compose Postgres container.
- **Script.** `scripts/dev-seed.sh` calls the route with curl.

## Alternatives considered

- **SQL seed scripts.** Fast, but they bypass the custody rules, the number counter, the timeline and audit classification. They also break with every schema change and cross module boundaries.
- **Extending the startup `DevelopmentSeed`.** It runs on every start, can't take parameters, can't target a new organization, and would slow startup at large factors.
- **A CLI command in the host.** It would need its own tenant, user, permission and audit setup outside the request pipeline, and a second entry point to maintain. The route reuses authentication and antiforgery, and can be called from the browser or curl.
- **Reset through the API.** That would need DELETE grants for `shelter_app` and break append-only ledgers and archive-not-delete. The script keeps deletion out of the runtime role.
- **Bogus/Faker library.** A new dependency (hard rule 9). Small curated Québec sample lists (`SampleData`) give the same realism, in French.

## Consequences

- Positive:
  - Seeded data goes through the same rules as real data.
  - Each future module adds one contributor, with no change to the runner.
  - Demo scale takes about 12 s locally and load scale about 80 s.
  - A run is reproducible by seed.
- Negative / accepted trade-offs:
  - One long transaction at large factors holds locks for the run. This is acceptable on a local database.
  - Audit events are written for every seeded row, and the audit trail shows the caller as the actor.
  - Repeated runs on one organization duplicate people with the same names.
  - The reset wipes every organization's module data, not just one.
  - A new organization is provisioned before the seed transaction, on the platform-admin connection. If the seed then fails, the organization stays, empty.
  - The reset script trusts the local Docker daemon. It refuses to run when `DOCKER_HOST` is set or the Docker context is not the local default.
  - Contributor classes ship in the production build, inert, because the route and permission are absent there.
- Follow-ups:
  - New modules (adoption, medical, and others) add an `IDevSeedContributor` with a free `Order` after the modules whose IDs they read, and publish their IDs under a new `DevSeedKeys` key if later modules need them.
