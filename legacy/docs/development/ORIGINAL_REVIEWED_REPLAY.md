# Original reviewed history after correction

The synthetic reviewed-correction policy explicitly permits a new revision from an
approval-ready, accepted or overridden case when the actor has exact live CORRECT
authority. It performs fresh normalization, assessment and routing, with a new audit.
Previous human approval never carries over; a new human decision is required before
returning the corrected result. This is synthetic process configuration only.

A duplicate original intake resolves the last retained review snapshot for that exact
input revision. It returns the original assessment and its own review history, not the
newest corrected assessment. Replayed historical detail has no mutation controls.
The current work-case endpoint continues to return the current revision.

`GET /api/review/work-cases/{caseId}/outbound/{destinationId}/{messageId}` reads one
exact retained committed receipt under current READ and EXPORT case authority. It
verifies the receipt's case binding and returns its original result JSON. It creates
no delivery side effect. A caller cannot use a grant for another case to inspect it.

Real PostgreSQL API tests prove original review/outbound, subsequent corrected
assessment and new review/outbound, exact original input/review/receipt replay and
restart. All assessment/input/review/receipt rows remain append-only. Together with
the correction, clarification and German browser tests this closes the generic
historical-replay acceptance under #185; real institutional policy remains external.
