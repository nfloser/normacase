# Offline command-line adapter

Follow root AGENTS.md. Keep evaluation in RuleEngine and transport parsing in Serialization.
German presentation strings belong in Resources/Messages.resx. Do not leak exception
messages or case content through error output. Assessment date and platform version
are explicit. Read local files only; no network lookup, scripts or implicit clock.
Use synthetic examples and test the runner with actual files.
