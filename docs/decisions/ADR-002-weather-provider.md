# ADR-002: WeatherAPI Provider Contract

## Context

The assignment names WeatherAPI and explicitly lists `/v1/current.json` and `/v1/forecast.json`. WeatherAPI `forecast.json` also includes `current`, so the provider could technically satisfy the screen with one call.

The WeatherAPI key was supplied outside the repository and must be treated as compromised/public for repository hygiene purposes.

## Decision

Use `https://api.weatherapi.com` and build requests with the WeatherAPI credential query parameter redacted in documentation:

- `/v1/forecast.json?<credential>&q=Moscow&days=3&aqi=no&alerts=no&lang=ru`
- `/v1/current.json?<credential>&q=Moscow&aqi=no&lang=ru`

`days=3` is intentional because WeatherAPI counts today as the first day. The adapter supports `WeatherApi:UseSeparateCurrentEndpoint`; when enabled it calls current and forecast in parallel with `Task.WhenAll`. When disabled, current data comes from the forecast response.

Provider DTOs are internal to Infrastructure and map to provider-independent Application/Domain models.

## Alternatives

- Use only `forecast.json`: lower latency and lower quota usage, but it does not demonstrate both endpoints named in the assignment.
- Use `days=4`: covers the UI but is wasteful and obscures provider semantics.
- Use `http://`: rejected because the API key is sent in the query string.

## Consequences

The implementation is honest about provider behavior while still satisfying the assignment. The second endpoint can be disabled later if latency/quota becomes more important than literal endpoint coverage.
