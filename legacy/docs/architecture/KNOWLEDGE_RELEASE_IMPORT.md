# Retained synthetic release import

The persistent synthetic workbench imports exact UTF-8 Knowledge JSON through a
PROPOSE-only endpoint. Decoded content is limited to 64 KiB; the separately bounded
JSON envelope accommodates escaped Unicode without increasing the artifact limit.
The existing strict pack loader validates the file. Only `SYNTHETIC` artifacts may
enter this host; import does not change lifecycle, validation, approval or activation.

Registration and the original import actor/time/hash commit in the live account
transaction. Duplicate exact content preserves its original import audit; a reused
pack/release identity with different bytes returns conflict. PostgreSQL rejects
updates/deletes of both artifacts and import audit. Startup-installed artifacts have
no invented import actor. They remain identifiable as installed artifacts.

Release listing uses a bounded, C-collated `(pack_id, release_id)` keyset cursor and
filters synthetic validation before the limit. Exact optional pack filtering makes
release history independently pageable. Loaded artifacts are hash/metadata verified;
listing never substitutes a latest release. Authenticated detail returns the exact
original JSON, hash and retained import provenance.

German UI keeps file content in memory, decodes UTF-8 strictly, preserves original
whitespace and decimal tokens, renders JSON as text, and clears the file input. It
selects the exact imported/retained release for the existing source/impact/test evidence
and distinct proposal/review/activation flow. Review-only users can inspect retained
releases but cannot import. Logout/unmount/401 abort requests and remove the component;
no browser storage or automatic retry is used.

An imported pack does not modify anonymous preview, installed field presentation or
live intake. The separate exact activation consumption boundary requires compatible
adapter configuration and explicit restart. Structurally different synthetic packs can
still demonstrate independent retained governance without inventing a real MD mapping.

PostgreSQL/API tests prove exact UTF-8 retention, original import provenance, concurrent
idempotency, immutable identity conflict, restart, two structurally different packs,
bounded release paging and denied actor spoofing/import. The real German browser
journey imports a release, verifies exact content/hash, proposes it with retained
evidence and completes separate review/activation without browser storage.
