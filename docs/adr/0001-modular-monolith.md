# 0001. Modular monolith with vertical modules

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

A small team is building a multi-tenant SaaS with nine business areas (architecture §6). These areas share one tenant model and one audit trail, and some writes must cross module lines in a single transaction (§7.1). Deployments and operations have to stay cheap. We still need enforceable boundaries so the codebase doesn't turn into a ball of mud (§2.1, §5.2, §5.3).

## Decision

- **Deployment unit:** one ASP.NET Core process (`Shelter.Host`) with one PostgreSQL database.
- **Organization:** code is organized by **vertical module** (Animals, People, Movements, Medical, Operations, Engagement, Municipal, Reporting, Platform), not by horizontal layer.
- **What each module owns:** its domain, features (endpoint + handler + validator colocated), persistence configuration, PostgreSQL schema, and permissions.
- **Cross-module access:** only through the other module's `.Contracts` project, events, or IDs (ADR 0015). `Shelter.ArchitectureTests` enforces this.
- **Cross-cutting concerns** (tenancy, persistence, auditing, jobs, documents, communications, integrations) live in BuildingBlocks. Communications is a building block, not a module.

## Alternatives considered

- **Microservices**: independent deployability we don't need yet. It would bring distributed transactions (which break §7.1), per-service tenancy plumbing, and a much larger operations bill.
- **Horizontal layers** (Api/Application/Domain/Infrastructure): every feature change would touch four projects, and nothing would stop one module from reaching into another's tables.
- **Monolith without enforced boundaries**: boundaries erode silently. Splitting a module out later would become a rewrite.

## Consequences

- Positive: one deploy and one transaction scope. Refactors across modules are cheap. Boundaries are tested on every build.
- Negative / accepted trade-offs:
  - All modules scale together.
  - One bad deploy takes down everything.
  - Boundary discipline depends on architecture tests and review, not on the network.
- Follow-ups: extracting a service needs a demonstrated operational or organizational need, plus a new ADR.
