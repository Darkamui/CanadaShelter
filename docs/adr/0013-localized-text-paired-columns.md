# 0013. Bilingual reference data as paired columns (`LocalizedText`)

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

The product is Québec-first and bilingual, with fr-CA as the default (§11.1). Reference data (breeds, intake and outcome reasons) and tenant-configurable content (forms, templates, public descriptions) must exist in both French and English (§11.2). Exactly two locales are in scope.

## Decision

- **Value object:** bilingual stored values use a `LocalizedText` value object (`Fr`, `En`).
- **Storage:** it maps to **two columns** on the owning table (e.g. `name_fr`, `name_en`) as an EF Core complex type.
- **Resolution at read time:** requested locale, then fr-CA, then en-CA.
- **Search** indexes each column with its own language configuration (ADR 0014).
- **UI strings** are not stored data. They live in per-feature i18next catalogs (`fr-CA.json` / `en-CA.json`) in the frontend. ESLint forbids hard-coded JSX strings.

## Alternatives considered

- **Translation table** (`entity_id`, `locale`, `value`): supports any number of locales, but every read needs a join. Two locales don't justify the extra complexity.
- **JSONB `{ "fr": …, "en": … }`**: flexible, but weaker constraints ("French required" is harder to enforce), and indexing and search are more awkward.
- **Resource files for reference data**: tenants couldn't add or override values.

## Consequences

- Positive:
  - Simple SQL, NOT NULL constraints where required, and a straightforward per-language search index.
- Negative / accepted trade-offs:
  - Adding a third language means a migration on every bilingual table.
  - Wide tables when an entity has several bilingual fields.
- Follow-ups: M1 defines `LocalizedText` and the global + tenant-override reference-data pattern (done: ADR 0017).
