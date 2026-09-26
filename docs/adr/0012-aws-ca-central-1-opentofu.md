# 0012. AWS `ca-central-1`, multi-AZ, no cross-region replication; OpenTofu

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Customers are Québec shelters and municipalities. Law 25 and customer expectations favour keeping personal information in Québec. Automatic geo-replication can silently move data out of the province (§16.1, §25.1, §25.2).

## Decision

- **Region:** production runs in **AWS `ca-central-1`** (Montréal area).
- **High availability:** multi-AZ within that region, not cross-region replicas.
- **No data leaves the region:**
  - Automated backups and snapshots stay in `ca-central-1`.
  - Cross-region snapshot copy is disabled.
  - Object-storage cross-region replication is disabled.
  - Any future off-site DR copy must target another Québec location, or be reviewed explicitly as an out-of-Québec transfer.
- **Hosting services:** containers on managed hosting, managed PostgreSQL, private S3. No Kubernetes initially.
- **Infrastructure as code:** **OpenTofu**, targeting `ca-central-1` only, in `infrastructure/deployment/`. Nothing lives only as console configuration.
- **Application code stays cloud-neutral:** S3-compatible storage and plain PostgreSQL, with MinIO serving locally.

## Alternatives considered

- **Azure Canada East** (Québec City): its paired region is Toronto, so geo-redundant storage and backups leave Québec unless explicitly disabled. Availability-zone and service coverage also has to be checked service by service.
- **AWS/Azure Toronto regions**: Canadian, but not Québec. Being in Canada isn't enough.
- **Terraform**: now under the BSL licence. OpenTofu is the open fork and stays compatible.
- **Local Québec hosting provider**: fewer managed services and more operations work.

## Consequences

- Positive: a clear residency story for privacy assessments and customer contracts.
- Negative / accepted trade-offs:
  - No protection against a regional disaster without a reviewed Québec-only DR copy.
  - Some AWS services may not be available in `ca-central-1`.
- Follow-ups:
  - Verify log and backup locations for every managed service before production.
  - Write the OpenTofu modules before commercial launch.
