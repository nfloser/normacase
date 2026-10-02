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

Validation level is separate from lifecycle. At minimum distinguish:
- `SYNTHETIC`
- `PUBLIC_REFERENCE`
- `DOMAIN_REVIEWED`
- `PRODUCTION_APPROVED`

## Sources

Productive domain rules require a traceable source with stable identity and metadata such as authority, title, document type, publication/validity dates, version, location, retrieval date and content hash.

Source `contentHash` is the SHA-256 digest of the exact retrieved bytes represented by that source revision. A hash supports identity and reproducibility; it does not establish authenticity, approval or trust by itself. NormaCase pack validation never fetches remote content during deterministic evaluation.

Non-synthetic Knowledge Packs require retrieval date and content hash metadata. Synthetic packs may omit these fields so test fixtures do not imply an external source retrieval that never happened.

Sources and rule versions are append/version operations, not in-place historical rewrites.

## Rule safety

Knowledge Packs are declarative data. They cannot contain executable scripts.

Unknown or missing values remain unknown. A rule must explicitly define how missing evidence affects evaluation; the platform must not invent a yes/no interpretation.

Conflicting or ambiguous source material is marked for domain review instead of silently resolved by developers.

## Public reference material

Rules derived from public official sources may be useful for reference implementations but remain `PUBLIC_REFERENCE` until separately reviewed by a qualified domain process.

## Tests and releases

A Knowledge Release must be internally consistent and validated before activation. Validation will grow to cover schemas, references, operators, validity intervals, dependencies, unreachable nodes, missing tests and manifest integrity.

Regression cases are synthetic.
