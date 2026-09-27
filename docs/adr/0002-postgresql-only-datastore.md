# 0002. PostgreSQL as the only datastore (no Redis in V1)

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

V1 needs relational data, row-level tenant isolation, full-text search, background jobs and caching (§5.1, §8, §12, §13, §8.5). Every extra datastore adds hosting cost, backup scope, residency review (§25.2) and another place tenant isolation can fail.

## Decision

- **PostgreSQL is the only stateful datastore** in V1.
  - Relational data lives there, isolated by RLS (ADR 0003).
  - Search uses built-in full-text search (ADR 0014).
  - Hangfire uses it for job storage (ADR 0010).
- **Cache:** in-process memory only, with keys that include `TenantId`.
- **Files:** go to private S3-compatible object storage. That is a blob store, not a datastore; MinIO serves locally.
- **Adding more stores:** Redis, a queue, or a search engine each need their own ADR and a measured requirement (CLAUDE.md hard rule 8).

## Alternatives considered

- **Redis** for cache, locks and the Hangfire backend: not justified at pilot scale, and it's one more service to secure, back up and keep in Québec.
- **Elasticsearch/OpenSearch**: better relevance tuning, but a second copy of personal data to isolate by tenant, keep in sync, and purge under Law 25.
- **A message broker** (RabbitMQ/SQS): in-process events plus Hangfire cover V1's async needs.

## Consequences

- Positive:
  - One backup/restore story and one residency review.
  - Transactions span data, jobs and (later) the outbox.
- Negative / accepted trade-offs:
  - PostgreSQL carries job polling and search load.
  - The in-process cache isn't shared across instances, so it must be short-lived or invalidation-tolerant.
- Follow-ups: watch job-table contention and search latency. If either becomes a measured bottleneck, revisit in a new ADR.
