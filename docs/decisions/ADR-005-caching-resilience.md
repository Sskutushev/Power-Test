# ADR-005: Caching And Resilience

## Context

WeatherAPI free plans are quota-limited. Blazor Server reconnects, user retry, prerender, and concurrent requests can multiply provider calls. External HTTP calls also need bounded latency and isolation from upstream failures.

## Decision

Use `Microsoft.Extensions.Http.Resilience` for typed HttpClient timeout, bounded retry, and circuit breaker. Retry applies only to transient network/5xx/408 outcomes; 401/403 and 429 are not retried.

Timeouts belong to the resilience pipeline, not to `HttpClient.Timeout`. A hard client timeout fires
across the whole retry sequence and surfaces as a raw `TaskCanceledException` instead of a classified
timeout, so `HttpClient.Timeout` is set to infinite and `AttemptTimeout` / `TotalRequestTimeout` carry the
budget. Circuit-breaker thresholds are configuration, not constants, so a test suite that deliberately
flips the provider between healthy and broken can neutralise the breaker instead of depending on test order.

Use `HybridCache` as a decorator over `IWeatherProvider` with key `weather:dashboard:v2`, and a second
decorator over `IRegionalWeatherProvider` with key `weather:region:v1`. Fresh lifetime is 5-15 minutes,
the stale fallback about an hour. `HybridCache` is preferred because it provides stampede protection
without a custom `SemaphoreSlim` implementation.

Reading the stale copy needs care: `HybridCache` exposes only get-or-create, so a naive read with a
factory returning `null` writes that `null` back and poisons the key for its whole TTL. The read uses
`DisableUnderlyingData | DisableLocalCacheWrite | DisableDistributedCacheWrite`, which turns
get-or-create into a genuine try-get.

Readiness health checks do not call WeatherAPI. An orchestrator polls readiness every few seconds; a probe
that reached the provider would burn the request quota and tie our availability to a third party. Readiness
validates local configuration only.

## Alternatives

- No cache: simplest, but wastes quota and makes Blazor lifecycle behavior costly.
- Raw `IMemoryCache`: acceptable for TTL, but requires extra request coalescing code.
- Redis/distributed cache by default: useful for multi-instance deployment, but unnecessary for the fixed-location single-instance test scope.
- Custom retry/circuit code: rejected because the platform package already solves this.

## Consequences

Provider calls are bounded, coalesced, and resilient. Multi-instance deployments can later add distributed cache without changing the Application use case.
