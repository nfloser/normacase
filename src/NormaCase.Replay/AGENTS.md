# Assessment application boundary

Inherit the repository engineering rules.

- Orchestrate validated knowledge, strict interchange and deterministic evaluation here.
- Require explicit assessment date and platform identity; never infer time or retrieve active knowledge during replay.
- Keep serialization free from engine orchestration and keep the rule/domain core free from application storage.
- Snapshots preserve original system output. Human review and storage approval are separate responsibilities.
- A checksum detects accidental alterations; it is not a signature, authorization, expert approval or an immutable store.
- No sensitive snapshots in logs, tests or repository files. Use synthetic examples only.

This module verifies portable interchange against embedded knowledge. It does not define a second assessment execution record, human-review record or persistence aggregate. The existing Application assessment-record boundary owns those responsibilities. Keep this dependency downstream of Serialization to avoid a cycle with Application record serialization.
