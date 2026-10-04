# Reviewed exact Knowledge activation

This is the durable headless governance boundary for #184. It supplements the
immutable release store; it does not change the synthetic host's configured
release selection. The authenticated administration adapter is described below.

`IReviewedKnowledgeActivationStore` accepts bounded immutable proposals containing
exact pack/release/SHA-256, source, impact-analysis and test-evidence references,
and explicitly supplied proposer/time. References identify retained evidence; this
adapter does not fetch external documents or claim that reference text proves a
medical review. A separately supplied reviewer records approval or rejection and a
reason. The proposer cannot decide their own proposal. Each proposal has one final
decision; rejection cannot be replaced with approval.

Activation requires an approved proposal, explicit actor/time and the exact expected
per-pack activation revision. Every proposal, review and activation verifies the
stored release's original bytes and metadata. A transaction lock serializes competing
activations even when a pack has no previous activation. Stale commands fail; they
do not rewrite history. Retrying a committed command with its old revision conflicts,
so callers reconcile using exact history before issuing a new command. Reverting to
an earlier approved release is an explicit new activation with the current revision.
No caller can request an implicit latest release.

Migration 011 stores proposals, decisions and activation revisions in separate
append-only tables. PostgreSQL rejects UPDATE and DELETE. Existing least-privilege
runtime grants (SELECT/INSERT) cover the new tables via migration-role defaults.
Audit UTC timestamps must have exact microsecond precision, matching PostgreSQL;
review cannot precede proposal, activation cannot precede review or the prior activation.
No hidden clocks, IDs or domain rules are introduced. Original assessments and release
artifacts remain unchanged. Technical approval/activation never upgrades SYNTHETIC
or PUBLIC_REFERENCE into DOMAIN_REVIEWED or PRODUCTION_APPROVED.

Real PostgreSQL tests exercise two structurally different synthetic packs, restart,
exact historical lookup, explicit rollback, rejected/unreviewed changes, hash mismatch,
self-review, stale/competing commands and mutation triggers. Application tests cover
bounded contracts and lossless audit timestamps. CI also rehearses migration/runtime
roles and synthetic dump/restore against the expanded schema.

Remaining integration: retained evidence/source management and explicit host
consumption of the selected activation. These are institution-independent implementation work. Actual domain
approval and institution-specific policy remain external inputs. #184 remains open.

## Authenticated synthetic administration

The persistent synthetic host now exposes `/api/review/knowledge/changes`, exact
change detail, `/decision` and `/activate`. Bearer authentication and live identity
suspension apply. Separate external assignments under
`SyntheticReview:KnowledgeAdministration` name existing configured synthetic users:
`PROPOSE`, `REVIEW`, `ACTIVATE`. Unknown users, unknown assignment keys and a shared
proposer/reviewer fail startup. Knowledge assignments confer no new case permissions
or identity-administration rights. The review-session response advertises permitted
Knowledge actions for presentation; each endpoint checks server configuration itself.

Proposal bodies contain only exact pack/release and bounded source/impact/test
references. The server creates the change ID, actor, timestamp and hash from an
existing verified SYNTHETIC artifact. Decisions accept only approval/reason; activation
accepts only the expected revision as an exact decimal string. Unexpected/duplicate
JSON keys, non-JSON, invalid UTF-8 and bodies over 4 KiB fail. The host cannot propose,
review or activate a non-synthetic artifact. The running host keeps its configured
release selection. Governance activation records are not hot-reload instructions.

The change list uses C-collated immutable ID keyset pagination, at most 25 displayed
records and a separate next-page cursor. Concurrent insertions before a cursor appear
on refresh, not retroactively on an already viewed page. Review status is loaded
currently; opening detail reads current activation independently. A stale activation
still fails under the write transaction. No history is deleted or hidden by pagination.
The German workbench supports proposal, approve/reject and explicit activation, displays
exact hash/evidence references/actor times and reloads detail after 409 without retry.
Credentials and data remain in React memory. Logout, unmount and 401 abort requests;
controller checks after JSON parsing reject delayed responses. Network errors never
resubmit proposals or decisions automatically: reconcile the saved list/detail first.

CI exercises real API/PostgreSQL authorization, 27-record pagination, server-owned
metadata, restart and stale activation, plus a two-user German browser journey and
delayed-list logout. Source document/evidence retention, controlled consumption of
activation by future intake and productive institution/domain approval remain open.
