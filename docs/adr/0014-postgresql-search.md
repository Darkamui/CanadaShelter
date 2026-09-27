# 0014. Search in PostgreSQL (`unaccent` + stemming + trigram)

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Staff search across animals, people, microchips, licences, cases and locations (§12). Searches must be accent-insensitive (`Éclair` = `eclair`) and work in French and English. Adding a search engine is ruled out for V1 (ADR 0002).

## Decision

- **Engine:** PostgreSQL full-text search with the `unaccent` and `pg_trgm` extensions. Both are created in the local database by `infrastructure/docker/postgres/init`.
- **Text-search configurations:** custom configurations combine `unaccent` with a stemmer, `fr_unaccent` and `en_unaccent`.
- **Free text:** builds its `tsvector` with the configuration matching the row's language, which comes from the record or the tenant default. When the language is unknown, both configurations are used.
- **Names and identifiers** (animal names, microchips, phone numbers, IDs) use normalized columns plus trigram matching, not stemming.
- **Indexes:** search indexes lead with `TenantId` where the index type allows it, and RLS applies to search queries as to any other query.

## Alternatives considered

- **Elasticsearch/OpenSearch**: better relevance and faceting, but a second store of personal data to isolate by tenant, keep in Québec, sync, and purge.
- **`ILIKE '%…%'`**: no stemming or accent handling, and no index support for leading wildcards without trigram.
- **Meilisearch/Typesense**: the same data-duplication and residency cost as Elasticsearch.

## Consequences

- Positive:
  - Search is transactionally consistent with the data, respects RLS, and needs no infrastructure.
- Negative / accepted trade-offs:
  - Relevance tuning is more limited.
  - The custom text-search configurations must be created by migration.
  - Large tenants may need careful index design.
- Follow-ups:
  - Create `fr_unaccent`/`en_unaccent` in the first migration that needs search.
  - Measure latency with realistic data.
