# Rules

## Process
- Work strictly by sprint scope. One response equals one sprint unless the user explicitly broadens scope.
- Before code: Goal / Files / Dependencies / Acceptance criteria.
- After code: changed files, why, commands executed, build/test result.
- After each implementation sprint: `dotnet build` and `dotnet test`. Red means do not continue.
- One commit should represent one meaningful step and use Conventional Commits.

## Code
- .NET 10, nullable enabled, TreatWarningsAsErrors, zero warnings.
- No unfinished-code markers, placeholder exceptions, or commented-out experiments in the final branch.
- Add abstractions only for external I/O, testing, architectural boundaries, unstable implementation details, or meaningful coupling reduction.
- Pass `CancellationToken` through Blazor -> MediatR -> Handler -> Provider -> HttpClient.
- No `new HttpClient()`, no `DateTime.Now` in domain/application logic, no `.Result`/`.Wait()`.
- Provider DTOs never cross the Infrastructure boundary.

## Credentials
- API key never appears in code, configs, tests, fixtures, logs, README, screenshots, or git history.
- Never log URLs with query strings because the WeatherAPI key is in the query.
- Before commits, inspect staged diffs for credential marker patterns configured for this repository.

## Tests
- Test behavior, not implementation details.
- Prefer a fake over a mock framework when the fake is simpler.
- Tests must not depend on machine time, timezone, network, or live provider state.
