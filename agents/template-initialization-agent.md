# Template Initialization Agent

**Version**: 1.0.0 | **Type**: Agentic Workflow | **Last Updated**: 2026-02-02

## Purpose

This agent specification defines an AI-assisted workflow for initializing a new project from the ProjectTemplate repository. It provides an interactive, guided experience as an alternative to the automated PowerShell script.

---

## Agent Overview

**Name**: `template-initialization-agent`  
**Trigger**: `@agent template-initialization-agent: Initialize new project`  
**Scope**: Repository-wide transformation from template to specific project  
**Duration**: Single-run (one-time initialization)

---

## Responsibilities

### Primary Objectives

1. **Gather Requirements**: Interactively collect project information from user
2. **Validate Input**: Ensure project name follows conventions (PascalCase, no spaces)
3. **Execute Transformations**: Rename files, update references, create projects
4. **Verify Consistency**: Ensure all references updated correctly
5. **Document Results**: Generate initialization record with steps completed
6. **Guide Next Steps**: Provide clear instructions for building first service

---

## Workflow Steps

### Step 1: Gather Project Information

**Prompt user for**:
```
Project Name (PascalCase, e.g., "RiskInsure", "FinanceHub"):
Organization Name (for CODEOWNERS, optional):
Initial Services (comma-separated, optional):
Reinitialize Git? (yes/no):
```

**Validate**:
- Project name matches `^[A-Z][a-zA-Z0-9]*$` (PascalCase)
- No spaces or special characters
- Not "ProjectTemplate" (must be different)

---

### Step 2: Confirm Transformation Plan

**Display summary**:
```
========================================
 Template Initialization Plan
========================================

Project Name:     {ProjectName}
Solution File:    {ProjectName}.slnx
PublicContracts:  {ProjectName}.PublicContracts
Root Namespace:   {ProjectName}

Files to Modify:
  - ProjectTemplate.slnx → {ProjectName}.slnx
  - Directory.Build.props (add RootNamespace)
  - README.md (replace ProjectTemplate references)
  - .github/copilot-instructions.md (replace references)
  
Files to Create:
  - platform/publiccontracts/{ProjectName}.PublicContracts.csproj
  - docs/TEMPLATE-INITIALIZATION.md
  
Git:
  {ReinitializeGit ? "Will remove history and create fresh repo" : "Will keep existing history"}

Continue? (yes/no):
```

**Require explicit confirmation** before proceeding.

---

### Step 3: Execute Transformations

**In order**:

1. **Rename solution file**
   ```
   ProjectTemplate.slnx → {ProjectName}.slnx
   ```

2. **Update Directory.Build.props**
   Add before `</Project>`:
   ```xml
   <PropertyGroup>
     <RootNamespace>{ProjectName}</RootNamespace>
   </PropertyGroup>
   ```

3. **Create PublicContracts project**
   ```bash
   cd platform/publiccontracts
   dotnet new classlib -n {ProjectName}.PublicContracts -f net10.0
   rm Class1.cs
   mkdir -p Contracts/{Commands,Events,POCOs}
   # Create README.md with project-specific content
   ```

4. **Add PublicContracts to solution**
   ```bash
   dotnet sln {ProjectName}.slnx add platform/publiccontracts/{ProjectName}.PublicContracts.csproj
   ```

5. **Update README.md**
   - Replace all instances of "ProjectTemplate" with "{ProjectName}"
   - Update repository URL placeholders
   - Update CODEOWNERS reference if organization provided

6. **Update .github/copilot-instructions.md**
   - Replace "RiskInsure" with "{ProjectName}"
   - Update example namespaces

7. **Optionally create initial services**
   If user provided service names, create folder structure:
   ```bash
   services/{servicename}/
   ├── src/{Api,Domain,Infrastructure,Endpoint.In}
   ├── test/{Api.Tests,Domain.Tests,Infrastructure.Tests,Endpoint.Tests}
   └── docs/domain-specific-standards.md
   ```

8. **Reinitialize git** (if requested)
   ```bash
   rm -rf .git
   git init
   git add .
   git commit -m "Initial commit from template - {ProjectName}"
   ```

---

### Step 4: Verify Consistency

**Check**:
- [x] Solution file exists: `{ProjectName}.slnx`
- [x] PublicContracts project exists: `platform/publiccontracts/{ProjectName}.PublicContracts.csproj`
- [x] PublicContracts in solution: `dotnet sln list` includes it
- [x] Directory.Build.props has `<RootNamespace>{ProjectName}</RootNamespace>`
- [x] README.md contains project name (search for "ProjectTemplate" should return 0)
- [x] No broken references in copilot-instructions.md

**Report any issues** found during verification.

---

### Step 5: Generate Initialization Record

**Create**: `docs/TEMPLATE-INITIALIZATION.md`

```markdown
# Template Initialization Record

**Project Name**: {ProjectName}  
**Initialized**: {DateTime}  
**Template Version**: 1.0.0  
**Agent Version**: 1.0.0

## Initialization Steps Completed

- ✅ Solution file renamed: ProjectTemplate.slnx → {ProjectName}.slnx
- ✅ RootNamespace set to: {ProjectName}
- ✅ PublicContracts project created: {ProjectName}.PublicContracts
- ✅ PublicContracts added to solution
- ✅ README.md updated with project name
- ✅ Copilot instructions updated with project name
{InitialServices ? "- ✅ Initial service folders created: {ServiceList}" : ""}
{ReinitializedGit ? "- ✅ Git repository reinitialized" : "- ⏭️ Git history preserved"}

## Verification Results

- Solution builds: {BuildStatus}
- PublicContracts compiles: {CompileStatus}
- No "ProjectTemplate" references: {CleanupStatus}

## Next Steps

1. **Create your first service**
   - Follow copilot-instructions/project-structure.md
   - Start with Domain layer (contracts, models)
   
2. **Configure local development**
   - Set up Cosmos DB emulator
   - Set up Azure Service Bus
   
3. **Define your domain**
   - Document terminology in domain-specific-standards.md
   - Design message contracts

## Reference Documentation

- Constitution: copilot-instructions/constitution.md
- Project Structure: copilot-instructions/project-structure.md
- Template Guide: docs/TEMPLATE-INITIALIZATION.md
```

---

### Step 6: Provide Next Steps Guidance

**Display**:
```
========================================
 ✓ Initialization Complete!
========================================

Project:          {ProjectName}
Solution:         {ProjectName}.slnx
PublicContracts:  {ProjectName}.PublicContracts

Next Steps:
  1. Review docs/TEMPLATE-INITIALIZATION.md
  2. Create your first service:
     - See copilot-instructions/project-structure.md
     - Use @agent to scaffold service structure
  3. Build solution: dotnet build
  4. Run tests: dotnet test

Documentation:
  - Constitution:      copilot-instructions/constitution.md
  - Project Structure: copilot-instructions/project-structure.md
  - Copilot Rules:     .github/copilot-instructions.md

Template Cleanup (Optional):
  You can now delete:
  - scripts/Initialize-Template.ps1
  - .github/template-instructions.md
  - agents/template-initialization-agent.md (this spec)
```

---

## Error Handling

### Invalid Project Name
```
Error: Project name must be PascalCase (e.g., "MyProject", "RiskInsure")
Invalid: "{input}"
Reason: {specific validation failure}
```

### Files Already Modified
```
Warning: This repository appears to have already been initialized.
Found: {ProjectName}.slnx (expected ProjectTemplate.slnx)

Continue anyway? This may cause unexpected results. (yes/no):
```

### Build Failures
```
Error: Solution build failed after initialization.
Command: dotnet build
Output: {error details}

This may indicate:
  - Broken project references
  - Invalid namespace updates
  
Recommend: Manual review of .csproj files
```

---

## Invocation Examples

### Example 1: Basic Initialization
```
User: @agent template-initialization-agent: Initialize new project

Agent: I'll help you initialize a new project from this template.

Project Name (PascalCase): RiskInsure
Organization (optional): AIS-Commercial-Business-Unit
Initial Services (optional): Billing, Payments
Reinitialize Git? (yes/no): yes

[Displays transformation plan]
[Executes steps 1-6]
[Reports completion]
```

### Example 2: Keep Git History
```
User: @agent template-initialization-agent: Set up FinanceHub

Agent: Project Name (PascalCase): FinanceHub
Organization (optional): 
Initial Services (optional): 
Reinitialize Git? (yes/no): no

[Preserves existing git history]
```

### Example 3: Error Recovery
```
User: @agent template-initialization-agent: Initialize "my-project"

Agent: Error: Project name must be PascalCase (e.g., "MyProject")
Invalid: "my-project"
Reason: Contains hyphens, starts with lowercase

Suggested: "MyProject"
Try again? (yes/no):
```

---

## Integration with Other Agents

### Handoff to Service Scaffolding Agent
After initialization, recommend:
```
To create your first service, use:
@agent service-scaffolding-agent: Create service "Billing"
```

### Handoff to Documentation Sync Agent
After services created:
```
To generate system documentation:
@agent documentation-sync-agent: Generate all system documentation
```

---

## Testing the Agent

### Test Case 1: Happy Path
```
Input: ProjectName="TestProject", Reinit=yes
Expected:
  - TestProject.slnx exists
  - TestProject.PublicContracts builds
  - No "ProjectTemplate" references
  - Git reinitialized
```

### Test Case 2: Preserves Git History
```
Input: ProjectName="TestProject", Reinit=no
Expected:
  - .git directory preserved
  - Commit history intact
```

### Test Case 3: Invalid Input
```
Input: ProjectName="test-project"
Expected:
  - Error message displayed
  - No files modified
  - Prompt to try again
```

---

## Agent Metadata

**Capabilities**:
- File renaming
- Content replacement
- Project creation via dotnet CLI
- Solution manipulation
- Git operations
- Build verification

**Dependencies**:
- .NET 10 SDK (for `dotnet new`, `dotnet sln`)
- Git (for reinitialization)
- PowerShell or Bash (for file operations)

**Execution Time**: ~30-60 seconds (depending on user input time)

**Idempotency**: ⚠️ NOT idempotent - Running twice will fail (no ProjectTemplate.slnx)

---

## Agent Limitations

1. **One-time use**: Cannot re-run on already initialized repository
2. **No rollback**: Cannot undo transformations automatically
3. **Manual verification**: Cannot guarantee all references updated (review recommended)
4. **Git conflicts**: If repository has uncommitted changes, may require manual resolution

---

## Related Documentation

- **[TEMPLATE-INITIALIZATION.md](../docs/TEMPLATE-INITIALIZATION.md)**: Manual initialization guide
- **[Initialize-Template.ps1](../scripts/Initialize-Template.ps1)**: Automated PowerShell script
- **[constitution.md](../copilot-instructions/constitution.md)**: Architectural principles
- **[project-structure.md](../copilot-instructions/project-structure.md)**: Service template

---

**End of Agent Specification**
