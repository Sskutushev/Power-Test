# ADR-003: Blazor Interactive Server Rendering

## Context

Blazor Interactive Server can prerender the component statically, then render again after the circuit connects. Loading data in `OnInitializedAsync` without protection can create two provider requests for one page open.

## Decision

Use Blazor Web App with Interactive Server rendering and `PersistentComponentState` for the weather page. The prerendered state is persisted into the interactive circuit to avoid a second MediatR/provider request. Infrastructure caching remains a second line of defense against duplicate circuit/retry activity.

## Alternatives

- Disable prerender with `InteractiveServerRenderMode(prerender: false)`: simplest and avoids duplicate load, but sacrifices the faster first frame.
- Rely only on cache/dedupe: reduces provider calls, but treats a UI lifecycle problem as an infrastructure concern.

## Consequences

The UI keeps the fast first render while preserving provider quota. Component tests must verify that one page open creates exactly one provider request.
