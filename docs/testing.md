# Testing strategy

The goal is confidence in behaviour, not a coverage number. Every level answers a different question, and
nothing is tested twice at two levels just to inflate the count.

## What can break, and where it is caught

| Failure mode | Caught by | Level |
|---|---|---|
| Wrong hourly window at a day boundary | `HourlyForecastSelectorTests` | Unit |
| "Remaining hours" computed against server time instead of Moscow time | Architecture rule banning the system clock + selector tests | Architecture, Unit |
| Provider DTOs leaking into the UI | Architecture rule on contract visibility | Architecture |
| A layer reaching sideways | Dependency-direction rules | Architecture |
| WeatherAPI payload misread (decimal vs integer, blank condition, missing icon) | Contract tests on live-shaped fixtures | Contract |
| Failure classified wrongly (timeout vs cancellation vs auth) | Contract tests per status code | Contract |
| Credential written to a log | `The_credential_never_reaches_the_logs` | Contract |
| Retrying something that must not be retried | Retry-policy contract tests | Contract |
| Misconfiguration discovered at the first user request instead of at startup | `Missing_credential_fails_fast` | Contract, Integration |
| Cold-cache burst multiplying provider calls | 50 concurrent readers → one upstream call | Integration |
| Internal detail leaking through an HTTP error | ProblemDetails body assertions | Integration |
| Readiness probe burning provider quota | `Health_probes_answer_without_calling_the_provider` | Integration |
| Missing security headers | `Responses_carry_baseline_security_headers` | Integration |
| Two provider calls per page open (prerender) | `Opening_the_page_issues_exactly_one_dashboard_query` | Component |
| Double click starting two parallel requests | `Double_click_on_refresh_...` | Component |
| Unmapped exception tearing down the Blazor circuit | `Unexpected_failure_is_contained_...` | Component |
| Horizontal page scroll on a phone | Mobile viewport scroll-width assertion | E2E |
| Retry not actually recovering | Failure → recovery journey | E2E |
| The app violating its own CSP | Console-message assertion during a full load | E2E |
| A benchmark silently measuring the wrong thing | `BenchmarkContractTests` | Performance |

## Levels

**Unit** (`Weather.Application.Tests`). Pure logic: the hourly selector and the two use cases. Fakes, not
mocking frameworks — the assertions are about call counts and returned queries, which a fake records
plainly. `FakeTimeProvider` supplies time.

**Architecture** (`Weather.Architecture.Tests`). NetArchTest plus source scans. These are the rules that
would otherwise erode silently over months.

**Component** (`Weather.ComponentTests`). bUnit against real Razor components. Assertions are about
rendered behaviour and accessible output — never CSS class internals.

**Contract** (`Weather.Infrastructure.Tests`). The real Infrastructure composition root against a local
WireMock peer. The production typed client, resilience pipeline, and mapper all run; only the network peer
is fake. Fixtures are shaped after real payloads and contain no credential.

**Integration** (`Weather.IntegrationTests`). `WebApplicationFactory` boots the real host. Only WeatherAPI
is replaced.

> A caveat worth knowing: configuration supplied by `WebApplicationFactory` is applied *after* `Program`
> runs, so anything read eagerly from `builder.Configuration` at startup is not overridable in tests.
> This is why rate limits are resolved per request from `IOptions` and the background refresh service is
> registered unconditionally and gated inside `ExecuteAsync`.

**E2E** (`Weather.E2ETests`). Chromium against a real Kestrel port, with WireMock as the provider. Nothing
reaches the internet. The browser fixture clears the weather cache between scenarios, and the circuit
breaker is neutralised for the suite — otherwise results would depend on test order.

**Performance** (`Weather.PerformanceTests`). BenchmarkDotNet targets for deserialisation, mapping, and
window selection. Network calls are not benchmarked: that would measure the stub, not this code. Each
target has a contract test asserting it still produces the expected result.

## Running

```powershell
dotnet test WeatherApp.slnx                                            # everything
dotnet test tests/Weather.Application.Tests                            # one level
dotnet test WeatherApp.slnx --collect:"XPlat Code Coverage"            # with coverage

# E2E needs a browser once:
pwsh tests/Weather.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Screenshots for the README are captured from a running instance and are skipped otherwise:

```powershell
docker compose up -d
$env:WEATHER_SCREENSHOT_URL = "http://127.0.0.1:8080"
dotnet test tests/Weather.E2ETests --filter FullyQualifiedName~DocumentationScreenshots
```

## Deliberate omissions

- **Mutation testing (Stryker)**: useful as a diagnostic, but it is a periodic activity rather than a
  gate, and it would double CI time for this codebase.
- **Load testing (NBomber / k6)**: the interesting number here is provider calls per page view, which the
  integration tests already assert exactly. A throughput figure would mostly measure the stub.
- **Snapshot tests of markup**: they would fail on every deliberate design change and pass on every real
  regression that keeps the markup intact.
