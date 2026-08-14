# Performance

Performance tests are intentionally separated from normal CI because load tests make pull request feedback slow and noisy.

Current performance project is a smoke-capable placeholder. The planned measurement scope is:

- BenchmarkDotNet for `HourlyForecastSelector`
- BenchmarkDotNet for WeatherAPI deserialization and mapping
- NBomber or k6 for warm-cache and cold-cache application throughput

No synthetic performance numbers are published until they are measured on a named machine/runtime.
