# ADR-004: Error Handling Model

## Context

WeatherAPI failures include transient outages, timeouts, invalid credentials, rate limits, malformed responses, and local configuration errors. UI and API consumers need stable behavior without seeing raw exception types or internal details.

## Decision

Application exposes a controlled failure taxonomy:

- Timeout
- Provider
- Auth
- RateLimit
- Protocol
- Configuration

Provider-specific exceptions carry `WeatherFailureKind`. UI maps the kind to human-readable Russian copy. The HTTP API maps the same kinds to `ProblemDetails` with stable status codes and a `traceId`.

No response exposes stack traces, API keys, full provider URLs, or internal provider messages.

## Alternatives

- Return raw exceptions to UI/API: rejected because it leaks implementation details and credentials.
- Use a `Result<T>` everywhere: viable, but exception flow is simpler for infrastructure failures and integrates naturally with ASP.NET Core exception handling.
- Collapse all provider errors into one unavailable message: simpler, but rate limit and configuration failures need distinct operator behavior.

## Consequences

User-facing failures stay predictable while logs preserve enough structured context for diagnosis. Tests cover exception translation and absence of credential leakage.
