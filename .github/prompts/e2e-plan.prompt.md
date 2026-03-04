---
agent: E2ETestPlanner
description: Build a requirements-to-tests plan from changed domain docs for centralized test/e2e Playwright coverage.
---

Use changed files under `services/*/docs/business/**` and `services/*/docs/technical/**` to:

1. Extract business flows, constraints, and state transitions.
2. Map each requirement to one or more E2E test scenarios.
3. Identify which existing helpers in `test/e2e/helpers/**` should be reused.
4. Produce a concise plan artifact in `test/e2e/generated/testplan.md`.

Output must include:
- Service name
- Source requirement files
- Scenario name
- Expected assertions
- Candidate generated spec path under `test/e2e/tests/generated/**`
