# Architecture

The application uses Clean Architecture with a vertical Application slice for the weather dashboard. Blazor UI and HTTP API both call the same MediatR query, so business orchestration is not duplicated.

```mermaid
flowchart LR
    UI[Blazor UI]
    API[HTTP API]
    MediatR[MediatR]
    Application[Weather.Application]
    Domain[Weather.Domain]
    Provider[IWeatherProvider]
    Infrastructure[Weather.Infrastructure]
    Redis[(Redis optional)]
    WeatherAPI[WeatherAPI]

    UI --> MediatR
    API --> MediatR
    MediatR --> Application
    Application --> Domain
    Application --> Provider
    Infrastructure --> Application
    Infrastructure --> Redis
    Infrastructure --> WeatherAPI
```

Provider JSON contracts are internal to Infrastructure. Application models are provider-independent and tested without HTTP or JSON.
