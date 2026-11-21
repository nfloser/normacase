# Rule engine rules

This file supplements the repository root `AGENTS.md` for the deterministic evaluator.

- Evaluation must be deterministic.
- No network access during evaluation.
- No implicit current-time dependency. Assessment date is explicit input.
- No arbitrary code execution, scripting or eval.
- `UNKNOWN` must not be coerced to yes/no, match/no-match or a positive outcome.
- Missing required facts fail closed as `INCOMPLETE`.
- Evaluation results require a structured Decision Trace.
- Trace data must identify the rule/version and source used.
- Keep the engine independent from ASP.NET Core, persistence and UI concerns.
- Add regression tests for every behavior change.
