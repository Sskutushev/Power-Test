# ADR-002: WeatherAPI Provider Contract

## Context

The assignment names WeatherAPI and explicitly lists `/v1/current.json` and `/v1/forecast.json`. WeatherAPI `forecast.json` also includes `current`, so the provider could technically satisfy the screen with one call.

The WeatherAPI key was supplied outside the repository and must be treated as compromised/public for repository hygiene purposes.

## Decision

Use `https://api.weatherapi.com` and build requests with the WeatherAPI credential query parameter redacted in documentation:

- `/v1/forecast.json?<credential>&q=55.7522,37.6156&days=3&aqi=no&alerts=no&lang=ru`
- `/v1/current.json?<credential>&q=55.7522,37.6156&aqi=no&lang=ru`

The `q` value is `LAT,LON`, matching the assignment literally. Coordinates also avoid the ambiguity of a
city name across providers and are what the territory map needs anyway.

`days=3` is intentional because WeatherAPI counts today as the first day. The adapter supports `WeatherApi:UseSeparateCurrentEndpoint`; when enabled it calls current and forecast in parallel with `Task.WhenAll`. When disabled, current data comes from the forecast response.

Provider DTOs are internal to Infrastructure and map to provider-independent Application/Domain models.

## Alternatives

- Use only `forecast.json`: lower latency and lower quota usage, but it does not demonstrate both endpoints named in the assignment.
- Use `days=4`: covers the UI but is wasteful and obscures provider semantics.
- Use `http://`: rejected because the API key is sent in the query string.

## Payload shape

Two details of the live contract are not obvious from the documentation and were found by calling the
real API rather than by writing fixtures:

- `pressure_mb`, `humidity`, `daily_chance_of_rain`, and `chance_of_rain` arrive as **decimals**
  (`1013.0`), even though they read like integers. Binding them as `Int32` deserialises every
  hand-written fixture and fails against production. They are bound as `double?` and rounded on the way
  into the Domain.
- `condition.text` and `condition.icon` can be present but blank. Blank is treated as absent.

Both are locked in by contract tests using live-shaped fixtures.

## Consequences

The implementation is honest about provider behavior while still satisfying the assignment. The second endpoint can be disabled later if latency/quota becomes more important than literal endpoint coverage.
