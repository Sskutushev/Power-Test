# Observability

The question this has to answer in an incident is narrow: *is the provider degraded, is our cache doing
its job, and is anything slow?* Everything below exists to answer that, and nothing else was added.

## Logging

Structured throughout — message templates with named properties, never string interpolation, so events
stay queryable.

| Event | Level | Properties |
|---|---|---|
| `weather_request_started` | Information | `RequestName` |
| `weather_request_completed` | Information | `RequestName`, `ElapsedMs` |
| `weather_request_failed` | Warning | `RequestName`, `ElapsedMs`, exception |
| `weather_query_slow` | Warning | `RequestName`, `ElapsedMs` |
| `weather_provider_call` | Information | `Path`, `StatusCode`, `ElapsedMs` |
| `weather_stale_served` | Warning | `StaleSince` |
| `weather_region_partial` | Warning | `Available`, `Requested` |
| `weather_region_point_failed` | Warning | `PointName`, exception |
| `weather_refresh_started/completed/failed` | Information / Warning | `Interval` |
| `weather_http_request_failed` | Warning | `TraceId`, `StatusCode`, `Path` |

**Never logged**: the request URI. The WeatherAPI credential travels in the query string, so logging a URL
would eventually record it permanently. The client logs the path, the status, and the elapsed time
instead, and a contract test asserts no log record contains the credential or the substring `key=`.

## Traces

OpenTelemetry with ASP.NET Core and `HttpClient` instrumentation, plus an application activity source
(`WeatherApp`) with spans around provider work:

- `weather.provider.dashboard`
- `weather.provider.region` (tagged with the point count)

Health probes, `/_framework`, and static assets are filtered out — otherwise they would dominate the
trace volume and hide the real traffic.

`traceId` appears in every `ProblemDetails` body, so a user-reported failure maps directly to a trace.

## Metrics

| Metric | Type | Tags |
|---|---|---|
| `weather.query.duration` | Histogram (ms) | — |
| `weather.provider.duration` | Histogram (ms) | `path`, `status` |
| `weather.provider.failures` | Counter | `kind` |
| `weather.query.failures` | Counter | `request` |
| `weather.cache.hits` / `weather.cache.misses` | Counter | `cache` |
| `weather.cache.stale_served` | Counter | `cache` |
| `weather.refresh.executions` | Counter | `outcome` |

Plus the standard ASP.NET Core, `HttpClient`, and .NET runtime instrumentation.

## Exporting

The exporter is not bound to a vendor. OTLP export switches on when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
and is simply absent otherwise, so the application never depends on a collector being present:

```powershell
docker compose up -d
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
```

## Health

| Endpoint | Answers |
|---|---|
| `/health/live` | The process is running. No checks are executed at all |
| `/health/ready` | Configuration is valid and the app can serve traffic |

Readiness deliberately does **not** call WeatherAPI. An orchestrator polls it every few seconds; a probe
that reached the provider would burn the request quota and make our availability depend on a third party's.
An integration test asserts that neither probe produces a provider call.

## What would be worth alerting on

- `weather.provider.failures` by `kind` — a rising `Auth` count means the credential is dead, which no
  amount of retrying will fix; a rising `Provider` count is an upstream outage.
- `weather.cache.stale_served` above zero for a sustained period — users are looking at old data.
- p95 of `weather.provider.duration` approaching the attempt timeout — the circuit is about to open.
- `weather.cache.misses` climbing without a matching traffic increase — cache lifetimes or key versioning
  regressed.
