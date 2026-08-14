# ADR-006: Territory Forecast Map

## Context

The assignment fixes the location to Moscow and says nothing about a map. A map was added because a
forecast is spatial data and a single column of numbers hides that; it also demonstrates the same
architecture holding up for a second, independent use case.

Any map raises three questions: what renders it, where the base tiles come from, and where the weather
overlay comes from. All three answers must be open, keyless, and safe to ship in a public repository.

## Decision

- **Renderer**: Leaflet 1.9.4 (BSD-2-Clause), vendored under `wwwroot/lib/leaflet`. It is deliberately not
  loaded from a CDN: a CDN is a third-party script origin, which the Content Security Policy forbids, and
  it would make the page depend on reaching that CDN.
- **Base tiles**: OpenStreetMap raster tiles (ODbL), attributed in the map control and in the page footer.
- **Precipitation overlay**: the public RainViewer radar index, which needs no key and no account. It is
  strictly optional — if the index cannot be read, the map still renders with the base layer and markers.
- **Weather points**: read through our own `GetRegionalWeatherQuery`, so the map goes through MediatR and
  the provider adapter like everything else. Points are configuration (`Weather:Region:Points`) and the
  whole feature can be switched off with `Weather:Region:Enabled`.

The map loads independently of the dashboard. It costs one provider call per point, so a map failure
degrades to an inline notice instead of taking the main screen down with it.

Because a canvas is not readable by assistive technology, the same data is also rendered as a text list
underneath the map.

## Alternatives

- **three.js or a 3D globe**: hundreds of kilobytes of dependency for decoration, with no gain in
  information density.
- **A commercial tile provider (Mapbox, Google)**: better cartography, but a second credential and a
  billable account attached to a test assignment.
- **OpenWeatherMap tile overlays**: good weather layers, but they require an API key — exactly what this
  decision avoids.
- **Server-rendered static map image**: simpler CSP story, but loses panning, zoom, and the radar loop.

## Consequences

The map adds one open-source dependency and zero credentials. Its provider cost is bounded by the point
count and the cache lifetime (nine points every fifteen minutes by default). If the point list grows, the
sweep is the first thing that should move to the background refresh service.
