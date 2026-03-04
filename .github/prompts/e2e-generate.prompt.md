---
agent: E2ETestPlanner
description: Generate runnable Playwright tests from requirement docs using existing test/e2e helper conventions.
---

Generate or update Playwright specs from requirement changes with these constraints:

- Write only under `test/e2e/tests/generated/**`.
- Reuse existing helpers from `test/e2e/helpers/**`.
- Keep tests deterministic.
- Include state/assertions derived from requirements (status transitions, identifiers, expected outcomes).

For each generated test:

1. Include source requirement file references in test metadata.
2. Include at least one baseline endpoint-health check.
3. Add one or more business-flow assertions from requirement text.
