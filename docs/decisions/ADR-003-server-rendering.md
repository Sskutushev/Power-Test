# ADR-003: Blazor Interactive Server Rendering

## Context

Blazor Interactive Server can prerender the component statically, then render again after the circuit connects. Loading data in `OnInitializedAsync` without protection can create two provider requests for one page open.

## Decision

Use Blazor Web App with Interactive Server rendering and disable prerender for the weather page using `InteractiveServerRenderMode(prerender: false)`. Infrastructure caching remains a second line of defense against duplicate circuit/retry activity.

## Alternatives

- `PersistentComponentState`: preserves a faster first frame and avoids duplicate load, but adds lifecycle complexity that is not justified for the first production-ready cut.
- Rely only on cache/dedupe: reduces provider calls, but treats a UI lifecycle problem as an infrastructure concern.

## Consequences

The UI preserves provider quota and keeps lifecycle behavior easy to reason about. The trade-off is losing the static prerendered first frame.
