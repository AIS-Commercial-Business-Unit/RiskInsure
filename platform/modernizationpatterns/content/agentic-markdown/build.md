---
title: "Build"
slug: "build"
summary: "Implements the solution through structured human-agent collaboration across code, tests, security, documentation, and architecture conformance."
---

## Why It Matters

Build is where the operating model turns into repeatable engineering work. It must balance acceleration with reviewability, traceability, quality, and control.

## Key Decisions

- What work can be delegated to agents and what must remain human-led?
- What artifacts will structure work decomposition and review?
- What standards govern code, tests, security, and documentation?
- How will context be supplied consistently to humans and agents?
- What evidence is required before work is considered ready?

## Major Activities

- Decompose work into structured implementation packets
- Generate and refine code, tests, and documentation
- Run architecture conformance and security checks
- Perform human review on risk-tiered changes
- Capture evidence packs for release readiness

## Human Roles

- Engineer
- Architect
- Reviewer
- Security engineer
- Platform engineer

## Agent Roles

- Coordinator agent
- Implementation agent
- Testing agent
- Security review agent
- Documentation agent

## Inputs

- Design artifacts
- Task briefs
- Architecture standards
- Context sources
- Tool access

## Outputs

- Working software
- Tests
- Security findings
- Documentation
- Evidence pack

## Artifacts

- Spec packets
- Test suites
- Review packets
- Code scans
- Traceability records

## Controls

- Branch protections
- Required reviews
- Static analysis
- Dependency scanning
- Evidence requirements

## Metrics

- Cycle time
- Review rework
- Test coverage
- Security issue discovery
- Agent-assisted completion rate

## Related Pillars

- platform-tooling
- human-agent-collaboration
- measurement-value
