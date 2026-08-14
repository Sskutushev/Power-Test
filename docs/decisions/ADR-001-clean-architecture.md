# ADR-001: Clean Architecture And MediatR

## Context

The application has one visible feature but includes an external provider, UI, HTTP API, resilience, tests, and operational concerns. Without explicit boundaries, WeatherAPI DTOs, configuration, and HTTP details can leak into UI and business logic.

MediatR is required by the assignment. MediatR v13+ has a commercial licensing model for larger organizations; this project uses it within the free/development scope. If licensing becomes a concern, a thin in-house `ISender` abstraction or Wolverine are the replacement candidates.

## Decision

Use four production projects:

- `Weather.Domain`: provider-independent domain records and value objects.
- `Weather.Application`: vertical-slice use cases, MediatR requests/handlers, contracts, errors, and pipeline behaviors.
- `Weather.Infrastructure`: WeatherAPI adapter, typed HttpClient, resilience, cache, configuration, and provider mapping.
- `Weather.Web`: Blazor Interactive Server UI and a minimal HTTP API over the same MediatR use case.

Allowed dependencies: Domain <- Application <- Infrastructure <- Web, plus Web -> Application. Application never references Infrastructure or Web.

Use only two MediatR pipeline behaviors initially: logging and performance. FluentValidation is not added because the first query has no user input to validate.

## Alternatives

- Single project: faster to start, but it hides architectural boundaries and makes provider DTO leakage likely.
- More granular projects per feature: too much ceremony for one bounded feature.
- Controllers/services without MediatR: simpler, but misses the assignment requirement and makes UI/API orchestration easier to duplicate.

## Consequences

The solution has enough structure to enforce dependencies and test the core use case in isolation. It also carries some project overhead, which is acceptable because each project maps to a real boundary.
