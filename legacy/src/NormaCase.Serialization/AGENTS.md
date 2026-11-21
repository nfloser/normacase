# Serialization boundary

Follow root AGENTS.md. Keep JSON/transport dependencies outside Domain and RuleEngine.
Preserve typed values, exact decimal precision, UNKNOWN and source revision metadata.
Reject malformed input without coercion. Use versioned contracts and round-trip tests.
Never log imported/exported assessment content. Serialization does not confer trust.
