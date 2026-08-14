# Performance

## What actually matters here

For a weather dashboard the dominant cost is not CPU — it is **how many times the provider gets called**.
That number is asserted exactly, not sampled:

| Behaviour | Provider calls | Asserted by |
|---|---:|---|
| Page open (cold cache) | 2 (`forecast.json` + `current.json`, in parallel) | Contract test |
| Page open (warm cache) | 0 | Integration test |
| Second read inside the cache window | 0 | `A_second_request_within_the_cache_window_...` |
| 50 concurrent cold-cache readers | 1 | `A_burst_of_concurrent_readers_produces_one_upstream_call` |
| Health probe | 0 | `Health_probes_answer_without_calling_the_provider` |
| Territory map refresh | 1 per configured point, once per 15 min | Regional provider tests |

Prerender is disabled, so a page open costs one query rather than two — the single most expensive default
in Blazor Interactive Server for a metered API.

## Microbenchmarks

Only pure computation on the request path is benchmarked. Network calls are not: measuring them would
report the stub's latency rather than this code's cost.

```
BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.9168)
AMD Ryzen 5 7535HS with Radeon Graphics, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.400, .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

| Method | Mean | StdDev | Allocated |
|---|---:|---:|---:|
| `DeserializeAndMapForecast` | 123.5 µs | 8.5 µs | 62.6 KB |
| `MapForecastOnly` | 27.3 µs | 2.8 µs | 18.8 KB |
| `SelectHourlyWindow` | 6.5 µs | 0.1 µs | 10.9 KB |

Input is a realistic three-day payload: 3 days × 24 hours.

**These are ShortRun numbers** (three iterations), taken to establish an order of magnitude rather than a
regression baseline. The error margins are correspondingly wide; a `--job medium` run is the right choice
before treating any of this as a threshold.

**Reading them.** The whole inbound path costs ~123 µs, of which ~96 µs is `System.Text.Json` and ~27 µs is
mapping. Against a provider call measured in hundreds of milliseconds, that is roughly 0.05 % of the
request. The hourly selection — the algorithm the assignment is actually about — costs 6.5 µs. There is
nothing here worth optimising, which is exactly what the measurement was for.

Source-generated JSON serialization is used, so there is no reflection-based deserialisation on the path.

## Running

```powershell
$env:WEATHER_RUN_BENCHMARKS = "1"
dotnet test tests/Weather.PerformanceTests -c Release --filter FullyQualifiedName~BenchmarkRun
```

Benchmarks are skipped by default and run on demand or through the manually triggered CI job. Each target
also has a contract test asserting it still produces the expected result — a benchmark that quietly starts
measuring the wrong thing is worse than no benchmark.

## Not measured, and why

- **Load testing (NBomber / k6)**: with a stubbed provider it would mostly measure the stub, and with the
  real provider it would burn quota to produce a number dominated by someone else's latency. The
  provider-call counts above are the meaningful throughput property, and they are asserted exactly.
- **Rendering benchmarks**: Blazor Server render cost is dwarfed by the network round trip.
- **Published latency figures for the deployed app**: none are given, because none were measured on a
  named environment. Invented numbers in a README are worse than no numbers.
