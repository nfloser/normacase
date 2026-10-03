# Knowledge governance

Domain knowledge is maintained separately from platform code.

## Lifecycle

A Knowledge change follows:

```text
Source
-> impact analysis
-> change
-> review
-> tests
-> approval
-> Knowledge Release
-> activation
```

Suggested lifecycle states are `DRAFT`, `IN_REVIEW`, `APPROVED`, `ACTIVE`, `DEPRECATED` and `RETIRED`.

Validation level is separate from lifecycle. An `ACTIVE` release is therefore not automatically domain-approved; it only means that release is active within its own validation class.

At minimum distinguish:
- `SYNTHETIC`
- `PUBLIC_REFERENCE`
- `DOMAIN_REVIEWED`
- `PRODUCTION_APPROVED`

## Sources

Productive domain rules require a traceable source with stable identity and metadata such as authority, title, document type, publication/validity dates, version, location, retrieval date, content hash and status.

From `PUBLIC_REFERENCE` upward, every source requires an explicit version, location, retrieval date and SHA-256 content hash. Format-v1 synthetic packs may omit version/location for compatibility, although repository fixtures include them. Synthetic fixtures remain explicitly synthetic and do not fabricate external retrieval metadata merely to satisfy a schema.

Sources and rule versions are append/version operations, not in-place historical rewrites.

Rule Trace preserves a detached immutable source revision snapshot, including all provenance metadata used for evaluation. Persist that snapshot with the assessment rather than resolving source IDs against the latest pack.

Content hashes describe original source document bytes. The validator checks metadata and hash syntax without fetching a location or verifying its content. A hash does not establish source authenticity; independent source review and future integrity verification remain necessary.

## Rule safety

Knowledge Packs are declarative data. They cannot contain executable scripts.

Unknown or missing values remain unknown. A rule must explicitly define how missing evidence affects evaluation; the platform must not invent a yes/no interpretation.

Conflicting or ambiguous source material is marked for domain review instead of silently resolved by developers.

## Public reference material

Rules derived from public official sources may be useful for reference implementations but remain `PUBLIC_REFERENCE` until separately reviewed by a qualified domain process.

## Release catalog and exact historical selection

`NormaCase.Knowledge.Catalog.KnowledgeReleaseCatalog` is the first storage-neutral
catalog boundary for retaining several validated releases of the same Knowledge Pack.

Registration accepts the original JSON text, validates it through the normal strict
Knowledge Pack loader and stores an immutable release artifact identified by the exact
`packId` + `releaseId` pair. The artifact records lifecycle status, validation level
and a lowercase SHA-256 fingerprint over the UTF-8 bytes of that exact registered JSON
text.

Selection is intentionally exact. The catalog does not infer "latest", "current" or
"active" from release ids, insertion order or lifecycle state. A caller requesting a
historical release must provide both identities explicitly.

The same exact JSON may be registered again idempotently. Reusing one pack/release
identity for different JSON is rejected instead of silently replacing history. Loading
an artifact reparses the retained JSON and returns a fresh validated `KnowledgePack`,
so mutation by one caller cannot alter the registered release seen by another caller.

The `KnowledgeReleaseCatalog` implementation is in-memory. A durable Application
store and PostgreSQL adapter retain the exact same artifacts across restart; the
opt-in persistent host registers and loads its installed synthetic releases through
that adapter. See [durable releases](../architecture/POSTGRESQL_KNOWLEDGE_RELEASES.md).
Its SHA-256 fingerprint distinguishes registered
content and detects accidental substitution; it is not a signature, authenticity
proof, approval record or tamper-evident store. Activation policy,
approval administration, signatures and key management remain separate boundaries.

## Tests and releases

A Knowledge Release must be internally consistent and validated before activation. Validation will grow to cover schemas, references, operators, validity intervals, dependencies, unreachable nodes, missing tests and manifest integrity.

Regression cases are synthetic.
