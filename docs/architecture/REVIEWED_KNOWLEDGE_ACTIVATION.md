# Reviewed exact Knowledge activation

This is the durable headless governance boundary for #184. It supplements the
immutable release store; it does not yet change the synthetic host's configured
release selection or expose authenticated administration endpoints or a workbench.

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

Remaining integration: authenticated server-owned actors, separate administration
permissions, bounded discovery of pending changes and history, German administration
views, retained evidence/source management and explicit host consumption of the selected
activation. These are institution-independent implementation work. Actual domain
approval and institution-specific policy remain external inputs. #184 remains open.
