## Plan: Rename Billing Bounded Context to PolicyEquityAndInvoicingMgt

Update the existing rename plan to add Terraform and infrastructure steps so container apps, Cosmos containers, Service Bus topology, outputs, and service configuration align with the new bounded context name and deployment pipeline.

**Steps**
1. Add a Terraform phase after Docker/local dev to cover platform/infra changes and state migration decisions for renamed resources. *depends on decision on Cosmos container name and endpoint queue names*
2. Update container app definitions in [platform/infra/services/billing-app.tf](platform/infra/services/billing-app.tf) to the new service name: resource blocks, app names, container names, image names, and any env vars that still embed Billing. Decide whether to keep or rename Cosmos container settings and align env vars accordingly.
3. Update service configuration maps in [platform/infra/services/variables.tf](platform/infra/services/variables.tf#L156-L175): rename the services map key from billing to policyequityandinvoicingmgt and align container_name and any other service-specific values.
4. Update infra outputs and references in [platform/infra/services/outputs.tf](platform/infra/services/outputs.tf) and any downstream consumers of the output name; decide whether to keep a compatibility output alias for billing_api_url.
5. Update infrastructure docs in [platform/infra/services/README.md](platform/infra/services/README.md) to reflect the renamed service, container app names, and service list.
6. Update Cosmos DB container resources in [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf#L68-L75) and [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf#L162-L169) based on the container-name decision. If renaming the actual container, plan data migration; if not, keep container names but consider renaming Terraform resource identifiers only.
7. Update Service Bus queues, topics, and subscriptions in [platform/infra/shared-services/servicebus.tf](platform/infra/shared-services/servicebus.tf#L63-L160) to the new endpoint and API send-only queue names. Align topic names with actual domain event names after the rename (or keep the old names if events remain unchanged).
8. Plan Terraform state migration: determine whether to use moved blocks or terraform state mv for renamed container app resources, queues, topics, and Cosmos containers to avoid destructive re-creates. *depends on step 2, 6, 7*
9. Add Terraform verification to the plan: run plan/apply in the appropriate environment and validate container app names, Service Bus artifacts, and Cosmos containers are correct. *depends on steps 2-8*

**Relevant files**
- [platform/infra/services/billing-app.tf](platform/infra/services/billing-app.tf) — container app resources, image names, env vars, app names
- [platform/infra/services/variables.tf](platform/infra/services/variables.tf#L156-L175) — services map entries for billing
- [platform/infra/services/outputs.tf](platform/infra/services/outputs.tf) — billing API output name
- [platform/infra/services/README.md](platform/infra/services/README.md) — infra documentation and service list
- [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf#L68-L75) — Billing container
- [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf#L162-L169) — billing-sagas container
- [platform/infra/shared-services/servicebus.tf](platform/infra/shared-services/servicebus.tf#L63-L160) — queue names, topic names, subscriptions for billing

**Verification**
1. Run Terraform plan/apply in the target environment and confirm container apps, queues, topics, subscriptions, and Cosmos containers match the new name and the intended compatibility decisions.
2. Validate Service Bus routing: queues and subscriptions exist for the renamed endpoint, and events/commands route as expected.
3. Validate Cosmos DB access: container name used by the service matches the container provisioned by Terraform.

**Decisions**
- Cosmos container rename: keep Billing (compatibility) vs rename to PolicyEquityAndInvoicingMgt (requires data migration and potentially new containers).
- Service Bus topic/queue naming: align with renamed namespaces vs keep event names if public contracts are unchanged.
- Terraform state migration: use moved blocks/state mv to preserve resources vs allow recreate in non-prod only.
- Output compatibility: keep billing_api_url as an alias or rename and update all consumers.

**Further Considerations**
1. If container app names and images change, align CI/CD and ACR image naming to avoid deployment mismatches.
2. If renaming Service Bus artifacts, coordinate deployment order across services to avoid message loss or stuck subscriptions.
