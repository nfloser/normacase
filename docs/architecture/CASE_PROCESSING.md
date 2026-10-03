# Case processing lifecycle

`CaseProcessingInstance` binds the existing opaque Workflow lifecycle to a typed
case id and an explicit immutable case-input revision. The Domain contains no
medical state enum or institution-specific route. Knowledge/configuration supplies
the `WorkflowDefinition` with its id/version, initial state and graph.

Two revisions have different responsibilities:

| Revision | Meaning |
| --- | --- |
| `CaseRevision` | The immutable normalized case input this process handles; positive and fixed for this instance. |
| `Revision` | The process transition sequence, starting at zero and incrementing once per explicit transition. |

`Start` preserves explicit identity and enters only the configured initial state.
`Apply` requires the expected process revision and an explicitly selected transition.
It delegates graph/terminal/version/overflow validation to `WorkflowInstance` and
returns a new case process without changing the earlier instance. In-memory
expected-revision validation does not replace a transactional store CAS operation.

New source input creates a new case revision and a separately initialized process.
An old process cannot be silently rebound to that input; the case-input revision has
no setter and transitions preserve it. The Application layer must select the current
input/process binding and invalidate readiness for older input before approving.

No assessment result is accepted by the Domain lifecycle API. In particular,
SUPPORTED does not cause approval, and an incomplete result is not a process state.
Application routing policy supplies a proposed explicit command; a later orchestration
boundary validates metadata, authorization and transactional revision before applying.

`Restore` checks structural graph membership and revision validity. It does not prove
that a transition history occurred, authenticate a actor or verify historical source
provenance. Full history/replay and graph/source immutability remain application and
storage responsibilities, as for existing Workflow run snapshots. No clock, generated
id, network, AI, persistence or vendor-specific concept enters this Domain class.

Synthetic tests demonstrate multiple opaque graphs for preparation, waiting for
information and integration-error states, plus immutable original state, terminal
rejection, invalid graphs, wrong version, stale revision and overflow handling.
