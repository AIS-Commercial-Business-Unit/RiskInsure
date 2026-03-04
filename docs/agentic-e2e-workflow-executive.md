# Agentic E2E Testing Workflow (Executive Overview)

## Why this exists

This workflow turns requirement changes into updated end-to-end (E2E) tests automatically, so testing stays aligned with business intent.

---

## Simple flow (starts with requirements change)

1. **User updates requirements**
   - Example locations:
     - `services/*/docs/business/**`
     - `services/*/docs/technical/**`

2. **GitHub Action detects those doc changes**
   - Workflow: `.github/workflows/agentic-e2e-generate.yml`

3. **Agent runtime reads the changed requirement files**
   - Runtime: `test/e2e/agent/generate-e2e-tests.mjs`
   - Triggered by script in `test/e2e/package.json` (`agent:generate`)

4. **Agent creates testing outputs**
   - Generated test files: `test/e2e/tests/generated/**`
   - Planning artifacts: `test/e2e/generated/**`

5. **Workflow opens an automated PR**
   - Branch: `automation/agentic-e2e-<run-id>`
   - Labels include `agent:e2e-generation` and `testing`

6. **Validation workflow checks generated tests**
   - Workflow: `.github/workflows/agentic-e2e-validate.yml`
   - PR mode: validates test discovery (`--list`)
   - Manual mode: can execute generated tests against configured API URLs

7. **Team reviews and merges**
   - Humans review generated tests before merge
   - This keeps governance and quality control in place

---

## What is “agentic” here?

The agent is a tool-using process that:
- inspects changed requirement documents,
- derives scenarios,
- generates Playwright tests using existing test helpers,
- and produces traceability artifacts (plan + summary).

It is **not** an uncontrolled runtime bot. It runs inside a scoped workflow with explicit inputs/outputs and review gates.

---

## Where the “project file” fits

The executable agent lives under the existing E2E project area:
- `test/e2e/agent/generate-e2e-tests.mjs`
- Invoked through `test/e2e/package.json` scripts

So, the current E2E project remains the host; no new top-level test framework was introduced.

---

## Guardrails built in

- Only requirement doc paths trigger generation
- Generated tests are restricted to `test/e2e/tests/generated/**`
- Existing curated tests are not overwritten
- Output is reviewed via PR before adoption

---

## Current trigger behavior (important)

Today, generation is configured on **push to `main`** for requirement doc paths (plus manual dispatch).

If desired, this can be adjusted to **PR-time generation** so test updates appear earlier in the change lifecycle.

---

## Executive outcome

This creates a clear chain from **business requirements → generated E2E tests → reviewable PR → validated test suite**, reducing manual test authoring lag while keeping control and auditability.
