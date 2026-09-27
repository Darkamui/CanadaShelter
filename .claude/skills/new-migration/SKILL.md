---
name: new-migration
description: Add an EF Core migration for the current issue, verify RLS and TenantId-leading indexes in the generated SQL, apply it to the local database, then run tenancy-check.
disable-model-invocation: true
argument-hint: <MigrationName>
---

# /new-migration <MigrationName>

Persistence follows ADR 0005: one composed `ShelterDbContext`, one migrations assembly, one history table (`platform.__ef_migrations_history`). Migration rules: `backend/CLAUDE.md` § Migrations.

## 1. Preconditions

- The branch is not `main`. The working tree contains only this issue's changes.
- Local Postgres is up: `docker compose -f infrastructure/docker/compose.yml up -d`.
- `SHELTER_MIGRATIONS_PROJECT` is set, or the default in `scripts/db-migrate-local.sh` names the migrations project. If neither is true, stop and ask.
- The name is PascalCase and describes the change (`AddAnimalIntake`), one migration per issue where possible.

## 2. Generate

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project "$SHELTER_MIGRATIONS_PROJECT" --startup-project backend/Shelter.Host
```

Never hand-edit a migration that exists on `main` (a hook blocks it). You may edit the new migration's `Up`/`Down` to add RLS SQL.

## 3. Verify the generated SQL

Render it with `dotnet tool run dotnet-ef migrations script <Previous> <MigrationName> --project … --startup-project backend/Shelter.Host`. For every **new tenant-owned table**, confirm:

1. `tenant_id uuid NOT NULL`
2. `ALTER TABLE … ENABLE ROW LEVEL SECURITY` **and** `FORCE ROW LEVEL SECURITY`
3. The tenant policy is created through the policy helper, not hand-written SQL.
4. Every index and unique constraint starts with `tenant_id`.
5. Grants to `shelter_app` cover only the privileges the feature needs.
6. No foreign key into another module's schema.
7. Bilingual columns come in pairs (`<name>_fr`, `<name>_en`), with NOT NULL where French is required.

A table without `tenant_id` must be intentionally global, justified in a comment, and hold no personal data.

Report each item as PASS or FAIL with the line number. Fix any FAIL before continuing.

## 4. Apply locally and test

```bash
./scripts/db-migrate-local.sh
dotnet test backend
```

Never run migrations against a non-local database. Never use `dotnet ef database update` directly (it is denied).

## 5. Hand off

Run `/tenancy-check`, then ask for the `tenancy-privacy-reviewer` agent on the diff (CLAUDE.md "How to work" step 6).
