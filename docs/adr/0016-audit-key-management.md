# 0016. Audit key management for crypto-shredding

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

ADR 0011 chose crypto-shredding for personal values in append-only stores (architecture §17.2). M1-6 has to settle how keys are held, bound and destroyed, and what happens when a key is missing, before any module stores personal data.

## Decision

- **Master key behind `IKeyProvider`.** `Audit:KeyProvider` selects it. The default, `Local`, reads a base64 256-bit `Audit:MasterKey` from configuration (development and tests). `AwsKms` is a stub until deployment (ADR 0012). The Host resolves the provider at startup, so a missing or invalid configuration fails fast in every environment.
- **One data key per tenant and subject** in `audit.person_data_key` (tenant-owned, RLS). It is created on first use, in the transaction that writes the audit row. The master key wraps it with AES-256-GCM, bound to `tenant|subject`.
- **The subject is declared in the model:** `HasAuditSubject(p => p.Id)` on a person, `HasAuditSubject(x => x.PersonId)` on a record about one. `audit_event.subject_id` names the key that a row's values use.
- **Stored form:** personal values become `{"$enc":"v1:<base64 nonce|ciphertext|tag>"}` (AES-256-GCM). The associated data is `tenant|subject|entity type|entity id|field`, so a value cannot be moved to another row or field.
- **Shred** (`ISubjectShredder`):
  - Sets `wrapped_key` to NULL and keeps the row as a tombstone. The shred itself is audited (`Shredded`).
  - A shredded subject never gets a new key: its later personal values are stored as `{"$redacted":"shredded"}`.
  - The runtime role can select and insert keys, and can update only `wrapped_key` and `shredded_at`. It cannot delete a key or move it to another subject.
- **Fail closed:** personal values are redacted, never stored in plain form, in all of these cases:
  - there is no subject;
  - there is no provider (platform-admin contexts);
  - the unclassified-field rule applies.
  A synchronous `SaveChanges` that would need a key throws: keys are fetched asynchronously.
- **Reading:** `IAuditReader` decrypts values for the current tenant. Values of shredded subjects read as `{"$unrecoverable":"shredded"}`. A permission check comes in M2.
- **Model rule:** an entity linked to a person (it has a subject, a personal field or a `…PersonId` property) must classify every field and declare a subject. A test over the composed model enforces this.

## Alternatives considered

- **Delete the key row on shred:** the next write for the subject would silently create a fresh key and start recording personal values again after an erasure request. A tombstone makes erasure sticky.
- **One key per tenant:** shredding one person would destroy every person's audit values.
- **Encrypt without associated data:** someone with SQL write access could copy a ciphertext into another row or field and have it decrypt there.
- **Envelope keys cached in memory across requests:** faster, but it adds invalidation on shred. Keys are fetched once per save or read for now.
- **Libraries (ASP.NET Data Protection, a KMS SDK now):** Data Protection rotates keys by itself and has no per-subject destruction. The KMS SDK is a new dependency, needed only at deployment.

## Consequences

- Positive:
  - Erasure is one row update.
  - Audit rows keep actor, action, field names and timestamps.
  - Misconfiguration fails at startup, not on the first personal-data write.
- Negative / accepted trade-offs:
  - Every save and every read that involves personal data costs one extra key query and one unwrap per subject.
  - Losing the master key loses all personal audit values.
  - Personal values written by platform-admin contexts are not recoverable; they are redacted.
  - `SaveChanges` must be async when personal data is involved.
  - Encrypted values can't be searched.
- Follow-ups:
  - The AWS KMS provider and master-key rotation (rewrap `wrapped_key`), with deployment.
  - The audit-read permission (M2).
  - Timeline events reuse the same protector when they land.
