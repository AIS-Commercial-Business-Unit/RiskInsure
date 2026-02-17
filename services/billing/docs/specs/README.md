# Billing Domain - Feature Specifications

This index lists all feature specifications that affect the **Billing** bounded context.

**Feature specs live in**: `/specs/###-feature-name/` (repo root)  
**This index provides**: Domain-specific view of features for Billing

---

## Documentation Philosophy

- **Domain docs** (`services/billing/docs/`) = **Current state** (living documentation of how Billing works today)
- **Feature specs** (`/specs/###-feature-name/`) = **Change slices** (specific additions/modifications in progress or completed)

When a feature ships, the domain docs are updated to reflect the new current state. Feature specs remain as historical records of intent and implementation decisions.

---

## Active Features (In Development)

*Features currently being developed that affect Billing*

<!-- Example:
- [001-invoice-cancellation](/specs/001-invoice-cancellation/spec.md) - Allow users to cancel unpaid invoices (Status: In Progress)
-->

No active features yet.

---

## Recently Shipped Features

*Features completed in the last 90 days*

<!-- Example:
- [003-multi-policy-billing](/specs/003-multi-policy-billing/spec.md) - Consolidated billing for multiple policies (Shipped: 2026-02-01)
-->

No recently shipped features yet.

---

## All Features (Historical)

*Complete chronological list of all features that touched Billing domain*

| Feature | Status | Description | Shipped Date |
|---------|--------|-------------|--------------|
| *No features yet* | - | - | - |

---

## How to Add a Feature

```bash
# 1. Create feature spec
/speckit.specify [your feature description affecting Billing]

# 2. After spec is created, add it to this index in "Active Features" section

# 3. After feature ships, move entry to "Recently Shipped" and update domain docs
```

---

## Related Domain Documentation

- [Overview](../overview.md) - Billing domain purpose and responsibilities
- [Business Requirements](../business/) - Business rules and processes
- [Technical Specifications](../technical/) - APIs, data models, integration patterns
- [Constitution](../../../../.specify/memory/constitution.md) - Non-negotiable architectural principles

---

**Last Updated**: 2026-02-16
