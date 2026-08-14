# ADR-007: Security Posture

## Context

The application is public, read-only, and unauthenticated, which narrows the threat model but does not
empty it. The realistic risks are: leaking the provider credential, letting third-party script into the
page, exposing internal details through errors, and running an over-privileged container.

The credential deserves special attention. It arrived as plain text in an email, so it has to be treated
as already compromised, and it travels in a query string, so it is one careless log statement away from
being permanently recorded.

## Decision

### Credential handling

The credential is supplied from outside the repository — user-secrets locally, an environment variable in
containers, a secret store in CI. It never appears in source, configuration, fixtures, screenshots, or
Docker layers. The typed client logs the request path, status code, and elapsed time, and never the
request URI. Two automated guards back this up:

- a contract test asserting that no log record contains the credential or the substring `key=`;
- an architecture test asserting that no committed configuration file carries a credential value.

### Transport

WeatherAPI is called over HTTPS even though the assignment shows an `http://` URL. Options validation
rejects a non-HTTPS base address, with a single explicit exception for loopback so contract and
integration tests can point at a local stub without a self-signed certificate dance.

### Browser surface

A Content Security Policy is applied that a Blazor Server app can actually satisfy: `script-src 'self'`
with no `unsafe-inline` and no `unsafe-eval`, plus an explicit allow-list of the image and connect origins
the map needs. This is why the theme bootstrap is an external module rather than an inline script, and why
Leaflet is vendored rather than pulled from a CDN. An end-to-end test asserts the application does not
violate its own policy. `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Permissions-Policy`, `Cross-Origin-Opener-Policy`, and HSTS are set alongside it.

### Error surface

Failures map to a closed taxonomy and are rendered as `ProblemDetails` with a trace id. No response
carries a stack trace, an internal type name, a provider message, or a hint that a failure was caused by
the credential — an authentication failure reaches the user as generic unavailability.

### Abuse

The public HTTP API is rate limited per client with a spec-compliant 429 and `Retry-After`. The Blazor
circuit is deliberately not limited: it is one long-lived connection per user, and limiting it would drop
live UI updates.

### Runtime

The container runs as a non-root user with all Linux capabilities dropped, no privilege escalation, and a
read-only root filesystem; writable paths are explicit `tmpfs` mounts. Data protection keys are persisted
to a mounted volume, which is what makes the read-only root filesystem possible. Redis is not published to
the host and runs unprivileged. CI fails on any vulnerable package, transitive ones included.

## Alternatives

- **No CSP**: the usual choice for Blazor, because a strict policy is fiddly. Rejected: the policy is what
  turns "no third-party script can run here" from an intention into a fact.
- **`unsafe-inline` for scripts**: would have allowed an inline theme bootstrap and saved one request.
  Rejected because it defeats the main purpose of the policy.
- **Authentication**: there is no user data and no mutating operation, so there is nothing to
  authenticate.
- **A secrets manager**: correct at scale, disproportionate for one credential in one deployment.

## Consequences

The credential has one supply path and two automated guards against leaking it. The browser cannot execute
foreign script. The container runs close to the minimum privileges it can. The cost is that adding any
third-party asset now requires an explicit, reviewed change to the policy — which is the intended friction.
