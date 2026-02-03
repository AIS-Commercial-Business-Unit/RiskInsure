# {Manager Name}

**Domain**: {Domain Name}  
**Aggregate Root**: {Aggregate Name}  
**Version**: {Version}  
**Last Updated**: {Date}

---

## Overview

**Business Purpose**: {What this manager does from business perspective}

**Responsibilities**: {High-level business responsibilities}

**Key Constraints**: {Major business constraints or invariants}

---

## Business Capabilities

This manager implements the following business capabilities:

1. [{Capability Name}](#capability-capability-name)
2. [{Capability Name}](#capability-capability-name)
   ... (list all public methods)

---

## Capability: {Capability Name}

### Business Purpose

{What this capability does in business terms}

### Business Rules

| Rule # | Business Constraint | Error Code | Retryable |
|--------|---------------------|------------|-----------|
| 1 | {Business rule description} | {ERROR_CODE} | {Yes/No} |
| 2 | {Business rule description} | {ERROR_CODE} | {Yes/No} |
| ... | ... | ... | ... |

### Expected Inputs

| Input | Business Meaning | Required | Constraints |
|-------|------------------|----------|-------------|
| {Input field} | {What it represents} | {Yes/No} | {Business constraints} |

### Expected Outputs

**Success**: {What happens on success}

**Failure**: {What happens on failure}

### Events Published

#### {EventName}

**Purpose**: {Why this event is published}

**When**: {Business trigger for this event}

**Key Information**:
| Field | Business Meaning | Example |
|-------|------------------|---------|
| {Field name} | {What it represents} | {Example value} |

**Subscribers**: {Who cares about this event - business perspective}

---

## Dependencies

| Dependency | Business Purpose | Type |
|------------|------------------|------|
| {Repository name} | {What business data it manages} | Repository |
| {Service name} | {What business capability it provides} | Service |
| Message Session | Publishing business events | Infrastructure |

---

## Business Examples

### Example 1: {Scenario Name}

**Scenario**: {Business scenario description}

**Given**: {Initial business state}

**When**: {Business action}

**Then**: {Expected business outcome}

---

## Change History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0.0 | {Date} | Initial documentation | {Name} |

---

*This documentation is maintained by the Document Manager Agent and should be regenerated when Manager code changes.*
