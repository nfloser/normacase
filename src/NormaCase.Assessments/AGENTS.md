# Assessment records and review provenance

Follow the root AGENTS.md.

This module owns immutable historical assessment metadata and human-review provenance.

- Never evaluate rules here. Decision behavior remains in NormaCase.RuleEngine.
- Accept only already-versioned interchange documents from NormaCase.Serialization.
- Preserve the exact serialized input/output text when creating a historical record.
- Derive assessment date, platform version, Knowledge Release and system outcome from validated documents; do not accept duplicate caller claims for them.
- Require explicit UTC timestamps. Do not read system time.
- Technical ids must be opaque and must not contain patient/domain meaning.
- Human review is append-only provenance. Never mutate or replace the original system assessment.
- Review metadata must not copy case facts, evidence or free-form source document contents.
- Persistence is an infrastructure concern introduced separately.
