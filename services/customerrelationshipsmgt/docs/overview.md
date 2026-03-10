# CustomerRelationshipsMgt Domain

## Purpose

The CustomerRelationshipsMgt domain manages **customer relationships, identity, and contact information**. This domain serves as the system of record for customer relationship data and provides information to other domains throughout the insurance platform.

---

## Key Responsibilities

1. **Relationship Management**
   * Create and maintain relationship profiles
   * Store relationship demographic information
   * Manage relationship identifiers and references

2. **Contact Information**
   * Maintain current addresses (mailing, physical, billing)
   * Store phone numbers and email addresses
   * Track communication preferences

3. **Customer Relationships**
   * Track customer-to-customer relationships (spouse, dependents, etc.)
   * Manage customer-to-policy associations
   * Support household and family groupings

4. **Relationship Lifecycle**
   * Handle new relationship onboarding
   * Process relationship information updates
   * Manage relationship status changes
   * Support relationship data privacy and deletion requests

---

## Integration Points

* **Policy**: Provides relationship information for policy creation and management
* **Billing**: Supplies billing contact information
* **Rating and Underwriting**: Shares relationship data for risk assessment
* **Fund Transfer Management**: Provides relationship identity for payment processing
* **Claims**: Supplies relationship contact information for claims processing

---

## Business Events

* `RelationshipCreated` - New relationship profile established
* `RelationshipInformationUpdated` - Relationship details modified
* `RelationshipContactInformationChanged` - Address, phone, or email updated
* `RelationshipClosed` - Relationship account closed

---

## Documentation Philosophy

**This document and all files in `docs/` represent CURRENT STATE** - the living truth of how the CustomerRelationshipsMgt domain works today.

**Feature specifications** (in `/specs/###-feature-name/`) capture **CHANGE SLICES** - specific additions or modifications being made. Once a feature ships, this domain documentation is updated to reflect the new current state.

**See**: [../../docs/SPEC-KIT-QUICKSTART.md](../../../docs/SPEC-KIT-QUICKSTART.md) for how specs and domain docs work together.

---

## Documentation Structure

* **Business**: Relationship data requirements, relationship types, data privacy rules, and retention policies
* **Technical**: APIs, data models, search capabilities, and integration specifications

---

## Domain Language

This service uses relationship-focused terminology:

- **Relationship** (not Customer): The primary domain object representing a customer relationship
- **RelationshipId**: Unique identifier with prefix "CRM-" (CustomerRelationshipsMgt)
- **Relationship Management**: Core capability for managing relationship data
- **Relationship Lifecycle**: States, transitions, and status management

This terminology distinguishes this bounded context from the original Customer service while maintaining semantic clarity.
