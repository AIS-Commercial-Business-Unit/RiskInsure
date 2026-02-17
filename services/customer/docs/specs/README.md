# Customer Domain - Feature Specifications

This index lists all feature specifications that affect the **Customer** bounded context.

**Feature specs live in**: `/specs/###-feature-name/` (repo root)  
**This index provides**: Domain-specific view of features for Customer

---

## Documentation Philosophy

- **Domain docs** (`services/customer/docs/`) = **Current state** (living documentation of how Customer works today)
- **Feature specs** (`/specs/###-feature-name/`) = **Change slices** (specific additions/modifications in progress or completed)

When a feature ships, the domain docs are updated to reflect the new current state. Feature specs remain as historical records of intent and implementation decisions.

---

## Active Features (In Development)

*Features currently being developed that affect Customer*

No active features yet.

---

## Recently Shipped Features

*Features completed in the last 90 days*

No recently shipped features yet.

---

## All Features (Historical)

*Complete chronological list of all features that touched Customer domain*

| Feature | Status | Description | Shipped Date |
|---------|--------|-------------|--------------|
| *No features yet* | - | - | - |

---

## How to Add a Feature

```bash
# 1. Create feature spec
/speckit.specify [your feature description affecting Customer]

# 2. After spec is created, add it to this index in "Active Features" section

# 3. After feature ships, move entry to "Recently Shipped" and update domain docs
```

---

## Related Domain Documentation

- [Overview](../overview.md) - Customer domain purpose and responsibilities
- [Constitution](../../../../.specify/memory/constitution.md) - Non-negotiable architectural principles

---

**Last Updated**: 2026-02-16
