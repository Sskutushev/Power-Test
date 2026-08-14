# Observability

Structured events:

- `weather_request_started`
- `weather_request_completed`
- `weather_request_failed`
- `weather_query_slow`
- WeatherAPI path/status/duration without query string

Useful metrics for a production deployment:

- provider duration histogram
- provider failures by kind
- cache hits/misses
- query duration
- stale responses served

Health endpoints:

- `/health/live`: process liveness
- `/health/ready`: local readiness; it does not call WeatherAPI
