# Build and verification of synthetic previews

The preview pipeline publishes self-contained, untrimmed .NET 10 API and CLI
executables for Windows x64 and Linux x64. The native runner for each target builds
the canonical frontend before publishing. No runtime project dependencies change.

```sh
npm --prefix frontend ci
npm --prefix frontend run build
python scripts/test_preview_integrity.py
python scripts/build_preview.py --rid linux-x64 --commit FULL_40_CHARACTER_GIT_SHA
python scripts/check_preview.py --archive artifacts/normacase-synthetic-preview-linux-x64.zip --rid linux-x64 --commit FULL_40_CHARACTER_GIT_SHA
```

Use `win-x64` on Windows. Existing output archives are never silently replaced.
The archive contains independent API/CLI runtime directories, bundled assets,
synthetic knowledge/examples, a platform-specific launcher, German start and pitch instructions
and a manifest. InformationalVersion is explicitly `0.1.0-preview+<full SHA>`.
The workflow checks out that exact SHA; previews of PR heads and merged main are
distinct builds. Archive identities are independent of Knowledge Release ids.

CI and preview workflows use workflow-scoped concurrency groups. When a newer commit
arrives on the same pull request, an older in-progress run of that same workflow is
cancelled as superseded. CI and preview do not cancel each other, different pull
requests remain independent, and the latest head still runs the complete configured
verification. This reduces duplicate private-repository runner consumption without
removing merge-relevant checks.

Verification extracts into a path with spaces, validates the complete file
inventory and SHA-256 digests, confirms included runtime configuration and starts
both executable hosts with shared runtime lookup disabled. It checks all five
German catalogs, local browser assets and security headers, cross-origin rejection,
exact decimal CLI/API capture and bidirectional snapshot replay. Test output
contains technical summaries only. Separate integrity tests cover corruption,
unlisted files, traversal and symlinks. Before any extraction, entry paths must be
canonical relative POSIX paths and portable to Windows: normalized aliases,
case-insensitive collisions, trailing dots/spaces, reserved device names, invalid
characters and file/directory prefix conflicts are rejected. This prevents two
individually hashed entries from overwriting the same extracted file. Extraction
uses a newly created temporary directory; this check is not a general-purpose
extractor for pre-existing or concurrently modified directory trees.
This is real process integration on both
operating systems, not a mocked publish test.

Only successful native checks upload ZIP/checksum artifacts. Artifacts expire after
14 days and are not signed releases. Download from the trusted project; checksums
alone are not authenticity guarantees. Self-contained runtime maintenance requires
rebuilding the entire package for .NET security updates. Linux native prerequisites
are listed in the German start guide. No Docker/database service or remote runtime
API is part of this preview.

Future release publication must archive the complete tested package and describe
known synthetic/security/domain limits. Production authentication, governed
knowledge activation, domain review and operator backup/restore remain separate
milestones; distributing a preview does not complete them.
