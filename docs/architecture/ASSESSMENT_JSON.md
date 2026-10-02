# Assessment JSON interchange

Use `NormaCase.Serialization.AssessmentJson` to save and restore assessment output.
Default System.Text.Json does not reconstruct the private read-only CaseValue
representation. Do not use it directly as a persistence contract.

`Serialize(result, platformVersion)` returns a format-v2 envelope with
`formatVersion`, explicit `platformVersion` and `assessment`. Core enums use
string names; case values use one of these exact representations:

```json
{"kind":"UNKNOWN"}
{"kind":"TRUTH","truth":"YES"}
{"kind":"TRUTH","truth":"NOT_APPLICABLE"}
{"kind":"NUMBER","number":12.5}
```

Domain output values use one of these exact representations:

```json
{"kind":"UNKNOWN"}
{"kind":"CHOICE","choice":"ELIGIBLE"}
```

Choice strings remain knowledge-defined technical data. `UNKNOWN` is reserved and cannot be used as a choice.

Truth NO is supported. Unknown has no implied number or boolean. Numbers use
decimal; strings are not coerced into numbers. JSON remains culture-invariant;
German display formatting belongs to the presentation layer.

`Deserialize(json)` restores typed trace values, evidence statuses, domain outputs and detached
source revision snapshots. Format v1 documents without domain outputs remain readable; v1 documents that try to carry the new `domainOutputs` member are rejected so the old contract cannot ambiguously acquire v2 semantics. It rejects unsupported formats, missing required
constructor arguments, unknown/duplicate properties, invalid value combinations,
numeric enum encodings, excessive depth and documents exceeding 8 Mi characters.

The adapter has no clock, file system, database or network dependency and does not
change the deterministic core. Serialization is not signing, source authenticity
verification, pack validation, assessment approval or an immutable database.
Importers must perform authorization and context validation before trusting an
external document. Exported traces can contain sensitive values in production;
store them under operator control and never log them wholesale. Repository tests
use synthetic data only.
