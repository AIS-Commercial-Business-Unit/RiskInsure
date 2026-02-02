# Documentation Sync Agent

**Purpose**: Maintain the top-level `docs/` folder with outward-facing documentation that reflects the current state of the RiskInsure system.

**Role**: Transform prescriptive rules (copilot-instructions) and domain specifics into descriptive documentation for external stakeholders, new team members, and operations.

---

## Scope

This agent maintains documentation in the **root `docs/` folder** covering:

### System-Level Documentation

- **`docs/architecture.md`** - High-level system architecture
  - Component diagram (Logic Apps, NServiceBus endpoints, Cosmos DB, Service Bus)
  - Data flow across services
  - Integration patterns
  - Technology stack overview

- **`docs/deployment-guide.md`** - Deployment procedures
  - Azure infrastructure setup
  - Container Apps configuration
  - Service Bus topic/subscription topology
  - Cosmos DB provisioning
  - Environment variables and secrets

- **`docs/api-catalog.md`** - HTTP API reference
  - All API endpoints across services
  - Request/response schemas
  - Authentication requirements
  - API versioning strategy

- **`docs/message-catalog.md`** - Message contracts reference
  - All public events and commands
  - Message flows between services
  - Routing and subscription rules

- **`docs/onboarding-guide.md`** - New developer guide
  - Repository structure explanation
  - Local development setup
  - Running tests
  - Debugging NServiceBus handlers
  - Common troubleshooting

- **`docs/observability.md`** - Monitoring and operations
  - Logging conventions
  - Application Insights queries
  - Common operational scenarios
  - Alerting strategy

---

## Source Material

The agent derives documentation from:

1. **Copilot Instructions** (`copilot-instructions/`)
   - `constitution.md` → Architecture principles
   - `project-structure.md` → Project organization
   - `.github/copilot-instructions.md` → Coding standards

2. **Domain-Specific Standards**
   - `platform/fileintegration/docs/filerun-processing-standards.md`
   - `services/Billing/docs/domain-specific-standards.md` (when exists)
   - `services/Payments/docs/domain-specific-standards.md` (when exists)

3. **Code Inspection**
   - Message contracts in `PublicContracts/` and service `Domain/Contracts`
   - API projects for endpoint discovery
   - Infrastructure projects for deployment configuration

---

## Trigger Commands

### Generate All Documentation
```
@agent documentation-sync-agent: Generate all system documentation
```

### Update Specific Document
```
@agent documentation-sync-agent: Update architecture.md to reflect current services
@agent documentation-sync-agent: Regenerate deployment-guide.md
@agent documentation-sync-agent: Update message-catalog.md with latest contracts
```

### Sync After Changes
```
@agent documentation-sync-agent: Sync docs after adding Payments service
@agent documentation-sync-agent: Update API catalog after adding invoice endpoints
```

---

## Output Specifications

### Architecture Diagrams
- Use Mermaid diagrams (renders in GitHub)
- Show services, endpoints, data stores, message flows
- Include component responsibilities

### Deployment Guides
- Step-by-step with Azure CLI or Bicep/Terraform
- Environment-specific configurations
- Prerequisites and validation steps

### API Documentation
- OpenAPI/Swagger-compatible format where possible
- Include example requests/responses
- Document error codes

### Message Catalog
- Group by domain (Billing, Payments, FileIntegration)
- Show message flow diagrams
- Include all required fields with types

---

## Documentation Principles

1. **Descriptive, Not Prescriptive**: Describe what IS, not what SHOULD BE (that's in copilot-instructions)

2. **External Audience**: Written for:
   - New team members
   - Operations staff
   - External stakeholders
   - Future maintainers

3. **Current State**: Always reflects the actual codebase, not aspirational design

4. **Examples Over Rules**: Show real examples from the codebase

5. **Diagrams First**: Use visuals extensively; text explains diagrams

6. **Maintained, Not Perfect**: Keep up-to-date over being comprehensive

---

## Agent Workflow

When triggered, the agent:

1. **Scan** - Review current codebase structure
   - Services in `services/` and `platform/`
   - Contracts in `PublicContracts/`
   - API endpoints in `*/src/Api`

2. **Extract** - Pull information from:
   - Constitution and project structure templates
   - Domain-specific standards files
   - Code (contracts, endpoints, models)

3. **Transform** - Convert to documentation:
   - Prescriptive rules → Architectural decisions
   - Code patterns → Deployment steps
   - Contracts → Message flows

4. **Generate** - Write/update markdown files in `docs/`
   - Use consistent formatting
   - Include diagrams where helpful
   - Add examples from real code

5. **Verify** - Check completeness:
   - All services documented
   - All public contracts listed
   - Deployment steps validate
   - Links work

---

## Example Output Structure

### `docs/architecture.md`

```markdown
# RiskInsure System Architecture

## Overview
RiskInsure is an event-driven insurance processing platform...

## Services

### Billing Service
**Responsibility**: Invoice generation and billing cycles
**Technology**: .NET 10, NServiceBus 10, Cosmos DB
**Endpoints**: 
- Billing.Api (HTTP API)
- Billing.Endpoint.In (Message processing)

[Component diagram here]

### Data Flow
[Sequence diagram showing message flow]

## Infrastructure
- Azure Container Apps for NServiceBus hosting
- Azure Service Bus for messaging
- Azure Cosmos DB (one container per service)
...
```

---

## Maintenance

**Frequency**: On-demand or when significant changes occur:
- New service added
- Major architectural change
- New API endpoints
- Infrastructure updates

**Responsibility**: Development team triggers agent; agent generates docs

**Review**: Generated docs should be reviewed for accuracy before commit

---

## Related Agents

- **Project Scaffolding Agent** - Creates new services following project-structure.md
- **Test Coverage Agent** - Verifies test coverage thresholds
- **Migration Agent** - Handles schema/contract migrations

---

**Version**: 1.0.0  
**Last Updated**: 2026-02-02  
**Owner**: Development Team
