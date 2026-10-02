# Assessment application boundary

Inherit the repository engineering rules.

- Orchestrate validated knowledge, strict interchange and deterministic evaluation here.
- Require explicit assessment date and platform identity; never infer time or retrieve active knowledge during replay.
- Keep serialization free from engine orchestration and keep the rule/domain core free from application storage.
- Snapshots preserve original system output. Human review and storage approval are separate responsibilities.
- A checksum detects accidental alterations; it is not a signature, authorization, expert approval or an immutable store.
- No sensitive snapshots in logs, tests or repository files. Use synthetic examples only.
