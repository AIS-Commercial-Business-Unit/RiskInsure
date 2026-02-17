Everything below is derived from that conversation; I’ve avoided adding generic Terraform advice that wasn’t explicitly discussed.

## 1. Repository & Root Structure Conventions

### Single repo, multiple projects

*   A single Git repo can safely contain **multiple Terraform projects** (infra stacks) without exploding into hundreds of repos.
*   The key is **clear root boundaries** and disciplined folder structure, not repo sprawl.

### One Terraform “root” per deployment context

*   Terraform **must be executed from the root folder** that contains:
    *   `main.tf`
    *   provider / versions file
    *   variables file
*   That folder defines the **execution boundary** for Terraform state and dependency resolution.

### Avoid running Terraform from arbitrary subfolders

*   Child folders are **not execution roots** unless explicitly treated as modules.
*   Running Terraform outside the intended root causes confusion around providers, versions, and state.

## 2. Isolating Services Using Modules (Primary Pattern)

### Each service = its own module

*   Every infrastructure “service” (VNet, Key Vault, RabbitMQ, Storage, DB, etc.) lives in **its own module folder**.
*   No shared “mega‑module” or catch‑all infra file.

Example (as discussed):

    infra/
      main.tf
      variables.tf
      outputs.tf
      modules/
        vnet/
          main.tf
          variables.tf
          outputs.tf
        keyvault/
          main.tf
          variables.tf
          outputs.tf
        servicebus/
          main.tf
          variables.tf
          outputs.tf

### Mandatory module files

For any module:

*   `main.tf` – resource definitions
*   `variables.tf` – inputs (with defaults or null placeholders)
*   `outputs.tf` – required so the root can consume values

Terraform **will not implicitly infer outputs** from modules; anything needed by the root must be explicitly output.

## 3. Root Module Responsibilities (What *Doesn’t* Go in Modules)

The root `infra/` folder:

*   Orchestrates modules
*   Wires dependencies between services
*   Defines shared concerns (tags, resource groups, locations)

The root **does not**:

*   Re‑implement service logic
*   Duplicate module internals
*   Contain giant monolithic resource blocks

This keeps the root readable and prevents Terraform plans from becoming unmanageable.

## 4. Environment Isolation Strategy (Single Repo)

### Environments are *not* separate repos

*   Dev / Test / Prod **do not require separate repos**.
*   They are isolated via **variable resolution**, not folder duplication.

### Environment-specific values via `.tfvars`

*   Environment overrides live in **TFVARS files**, which Terraform reads **last**.
*   “Last value wins” is relied on intentionally to override defaults safely.

Example pattern:

    infra/
      variables.tf        # defaults or null placeholders
      dev.tfvars
      test.tfvars
      prod.tfvars

Terraform resolution order was explicitly discussed:

*   Variables defined in `.tfvars` override those in `variables.tf`
*   The **last read file is authoritative**

## 5. Variable Design Conventions

### Defaults as placeholders, not real values

*   `variables.tf` often contains:
    *   `default = null`
    *   or safe placeholders
*   Real values live in `.tfvars`, not in module code.

This enables:

*   One codebase
*   Many environments
*   Minimal accidental drift

### Keep “edit surface” small

*   For large environments, teams are directed to **edit only the `.tfvars` file**, not the modules.
*   This reduces accidental architectural changes.

## 6. State & Refactoring Safety

### Moving files is allowed (with care)

*   Terraform code can be reorganized **after initial creation**.
*   When moving roots or modules:
    *   Re-run `terraform init -upgrade` to reconcile state and providers.

### Modules enable refactoring without breaking consumers

*   As long as module inputs/outputs remain stable, internals can evolve freely.
*   This supports long-lived repos without constant rewrites.

## 7. Branching & Change Safety

### Never work directly on `main`

*   Always create a **feature branch** before making Terraform changes.
*   If something breaks:
    *   Delete the branch
    *   Main remains deployable at all times

This was emphasized as a **hard rule**, especially when Copilot or automation is involved.

## 8. Copilot / AI-Specific Terraform Conventions

### Scope Copilot to a single Terraform root

*   Copilot should operate **only within the active infra directory**.
*   Explicitly tell it:
    *   “Work only in this folder”
    *   “Do not modify other directories”

### Index the repo before asking architectural questions

*   Explicitly ask Copilot to:
    *   “Index this repository and understand its structure”
*   This produces materially better and more accurate Terraform changes.

### MCP servers for current Terraform code

*   Using a Terraform MCP server ensures:
    *   Current provider syntax
    *   Correct resource schemas
*   This avoids stale or invalid Terraform output from the LLM.

## 9. How Services & Environments Come Together (Mental Model)

From the discussion, the intended model is:

*   **Modules** isolate *what* a service is
*   **Root** defines *how services work together*
*   **TFVARS** define *where and for whom they run*

That separation is what allows:

*   One repo
*   Many services
*   Many environments
*   Minimal chaos

## 10. Practical “Do / Don’t” Summary

### ✅ Do

*   One repo, many modules
*   One root per deployable stack
*   One module per service
*   TFVARS for environment isolation
*   Outputs everywhere
*   Branch-first Terraform changes

### ❌ Don’t

*   Duplicate repos per environment
*   Put real values in `variables.tf`
*   Build mega‑modules
*   Let Copilot roam the entire repo
*   Run Terraform outside the intended root

If you want, next we can:

*   Turn this into a **repo README / conventions doc**
*   Map this to **AIS reference architecture**
*   Or overlay **security guardrails** (which you explicitly called out as missing and important in the conversation)
