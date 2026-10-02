# ADR 0001: Modular monolith with a deterministic domain core

- Status: Accepted
- Date: 2026-10-02

## Context

NormaCase needs strong auditability and multiple domain areas, but the product is at an early stage. Premature service boundaries would add deployment, consistency and observability complexity without proven value.

Domain outcomes also need to be reproducible and independent of infrastructure details.

## Decision

Start as a modular monolith.

Keep the domain and rule core framework-independent. Rule evaluation receives all decision-relevant context explicitly and cannot depend on network access, implicit current time, generative AI or arbitrary dynamic code.

Domain knowledge is external, versioned data interpreted by the platform.

## Consequences

- Cross-module boundaries still need explicit contracts and dependency discipline.
- A single deployment is the default until a measured reason justifies separation.
- The core is straightforward to unit-test and can later be hosted by different adapters.
- New medical domains should be implemented as Knowledge Packs rather than core branches.
