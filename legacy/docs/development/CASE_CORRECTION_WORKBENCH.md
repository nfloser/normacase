# Synthetic correction workbench

Authenticated persistent review exposes exact history pages and snapshots at
`GET /api/review/work-cases/{caseId}/history[/{version}]`. The bounded page cursor
is a canonical string storage version, distinct from case/process/audit revisions.
Each historical snapshot includes the original assessment-record JSON and an optional
verified correction relation. Historical detail has no review actions.

`GET .../correction-context` returns the original normalized input and the current
policy and revision context under live READ authorization. Only configured CORRECT
case authority can submit `POST .../corrections`. The strict request names correction/
message identities, consecutive upstream revision, exact policy/version and expected
case/process/audit revisions, a bounded reason and original-precision input JSON.
Actor, platform version and receipt/recording time come from the trusted server.
The manual correction adapter preserves the upstream stream while recording its own
adapter identity. It does not pretend to be the original upstream sender.

Two synthetic policies cover information completion and correction during manual
review. Both use the exact retained original Knowledge release, deterministic
reassessment and fresh routing. Further human review is required for outbound delivery.
Unresolved inputs remain incomplete or in manual review.

The German workbench edits structured facts/evidence and date, preserving decimal
precision with lossless JSON. History opens a read-only detail; returning to the current
case is explicit. All requests are aborted and case data cleared on logout/unmount.
A stale correction returns conflict; a transport interruption is reported as unknown
completion and does not silently retry with new identities.

Real PostgreSQL API tests exercise both policies, spoofing/denial, exact history,
stale review, new approval, outbound and restart. Chromium exercises actual intake,
German correction fields, historical read-only behavior, new approval, outbound and
logout. Targeted clarification requests and resolution remain tracked by #185.
