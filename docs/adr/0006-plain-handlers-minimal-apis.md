# 0006. Handlers as plain classes, Minimal APIs, no MediatR

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Features are vertical slices that colocate endpoint, request/response, handler and validator (§5.2). We need a request pipeline that is easy to trace, keeps OpenAPI accurate for Orval (ADR 0008), and doesn't hide control flow behind reflection.

## Decision

- **Endpoints:** Minimal APIs, one file per feature. Each module maps its routes under `/api/{RoutePrefix}` through its `IModule` (explicit `ModuleCatalog`, no assembly scanning). Every endpoint has `.WithName(...)`, which becomes the operationId and the Orval hook name, and declares a permission.
- **Handlers:** ordinary classes registered in DI and injected straight into the endpoint delegate. No mediator, and no generic `IRequestHandler<,>` abstraction.
- **Cross-cutting behaviour:** endpoint filters or ASP.NET middleware (validation, ProblemDetails), or explicit calls, rather than pipeline behaviours.

## Alternatives considered

- **MediatR (or another mediator)**:
  - Indirection makes "go to definition" useless.
  - Pipeline behaviours hide ordering.
  - It adds a dependency whose licence changed in 2025.
  - It solves a decoupling problem a modular monolith with explicit contracts doesn't have.
- **MVC controllers**: more ceremony per endpoint, and they group by resource rather than by feature.
- **FastEndpoints / Carter**: extra dependencies for conventions Minimal APIs already cover.

## Consequences

- Positive:
  - Straightforward stack traces and navigation.
  - Accurate OpenAPI from typed results.
  - Fewer dependencies.
- Negative / accepted trade-offs:
  - Cross-cutting concerns have to be applied deliberately per group or endpoint, not through one global pipeline.
  - A little DI registration boilerplate per module.
- Follow-ups: M2 adds a `FallbackPolicy`, so an endpoint with no declared permission is denied by default.
