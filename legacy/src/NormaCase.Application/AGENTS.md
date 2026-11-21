# Application layer

Follow the root `AGENTS.md`.

This layer orchestrates deterministic domain/rule behavior. It may compose Domain,
Knowledge and RuleEngine types, but must not duplicate or reinterpret rule semantics.

Assessment execution metadata such as ids and recording timestamps is supplied
explicitly by callers. Do not hide system time, random id generation, network access
or persistence behind application services.

Historical-record models are storage-neutral. Database, web and UI concerns belong
in adapters. Snapshot mutable caller input before evaluation/record creation and keep
UNKNOWN/evidence states explicit.
