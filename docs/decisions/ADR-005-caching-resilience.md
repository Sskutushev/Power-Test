# ADR-005: Caching And Resilience

## Context

WeatherAPI free plans are quota-limited. Blazor Server reconnects, user retry, prerender, and concurrent requests can multiply provider calls. External HTTP calls also need bounded latency and isolation from upstream failures.

## Decision

Use `Microsoft.Extensions.Http.Resilience` for typed HttpClient timeout, bounded retry, and circuit breaker. Retry applies only to transient network/5xx/408 outcomes; 401/403 and 429 are not retried.

Use `HybridCache` as a decorator over `IWeatherProvider` with key `weather:moscow:v1`. Fresh cache lifetime starts at 5 minutes, stale fallback at about 1 hour. `HybridCache` is preferred because it provides stampede protection without a custom `SemaphoreSlim` implementation.

Readiness health checks do not call WeatherAPI. They validate local configuration and cache availability only.

## Alternatives

- No cache: simplest, but wastes quota and makes Blazor lifecycle behavior costly.
- Raw `IMemoryCache`: acceptable for TTL, but requires extra request coalescing code.
- Redis/distributed cache by default: useful for multi-instance deployment, but unnecessary for the fixed-location single-instance test scope.
- Custom retry/circuit code: rejected because the platform package already solves this.

## Consequences

Provider calls are bounded, coalesced, and resilient. Multi-instance deployments can later add distributed cache without changing the Application use case.
