# Local HTTP adapter

Follow the root AGENTS.md.

This project is a local development adapter for synthetic data. It is not a production patient-data endpoint.

- Bind only to loopback interfaces.
- Reject non-local Host and Origin values before endpoint execution.
- Do not enable CORS, forwarded headers, runtime network fetching, telemetry or request-body logging.
- Keep evaluation in NormaCase.RuleEngine and interchange JSON in NormaCase.Serialization.
- Reuse the versioned CaseInput/Assessment JSON contracts; do not invent parallel transport semantics.
- Bound request bodies and reject malformed input without coercion.
- Human-readable errors belong in German resources and must not expose case values, paths or exception text.
- Add real HTTP integration tests for security boundaries and endpoint behavior.
