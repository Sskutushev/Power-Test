# Testing

| Risk | Test level |
|---|---|
| Wrong hourly forecast boundaries | Application unit tests |
| Machine timezone changes behavior | Application unit tests and architecture scan |
| Provider DTO leaks | Architecture tests |
| WeatherAPI JSON shape changes | Infrastructure contract tests |
| DI registration breaks | Integration tests |
| UI state regressions | bUnit component tests |
| Browser/mobile regressions | Playwright E2E |
| Cache stampede | Integration tests |
| Hot-path regressions | BenchmarkDotNet/NBomber sprint |
