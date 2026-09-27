# 0011. Crypto-shredding for personal data in append-only stores

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Audit events and timeline events are append-only (§7.1, §17.4). Law 25 requires retention limits and the destruction or anonymization of personal information (§16.6, §16.7). If an append-only log stores raw personal values, those values survive the purge of the source record (§17.2).

## Decision

- **Classification:** fields are classified as personal or non-personal in entity configuration. Logs already use the same `[PersonalData]`/`[NonPersonalData]` taxonomy (`BuildingBlocks/Logging`).
- **Non-personal values** are stored in plain form in audit and timeline payloads.
- **Personal values** are encrypted with a **per-person data key**. Data keys are encrypted by a master key held in managed key storage (KMS). Locally, a file- or env-based master key is used behind `IKeyProvider`.
- **Purge:** purging or anonymizing a person destroys their data key. The audit rows remain (actor, action, field names, timestamps), but the personal values are unrecoverable.
- **Scope:** the same rule applies to every append-only store that may hold personal data.
- **Build-time check:** unclassified fields on Person-linked entities fail the architecture test.

## Alternatives considered

- **Rewrite or delete audit rows on purge**: breaks append-only integrity and the audit's evidentiary value.
- **Never store personal values in audit** (field names only): loses before/after evidence for sensitive changes, which §17.4 requires.
- **Tokenization vault**: an extra service and lookup on every read. Crypto-shredding gets the same effect with keys we already have to manage.

## Consequences

- Positive:
  - Purges are a single key deletion.
  - The audit trail stays complete and tamper-evident.
- Negative / accepted trade-offs:
  - Key management becomes critical: losing the master key means losing all personal audit values.
  - Encrypted values can't be searched or indexed.
  - Reading audit history costs a decryption per person.
  - Every new personal field must be classified.
- Follow-ups: M1-5 implements the classification and the audit interceptor; M1-6 the key store and shredding (ADR 0016). The production KMS provider is added with deployment.
