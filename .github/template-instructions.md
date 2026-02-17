# Using This Template Repository

**Quick Reference Guide** | **Last Updated**: 2026-02-02

This file provides quick instructions for using this repository as a GitHub template.

---

## ✅ You're Using a Template!

This repository is designed to be **copied and customized** for new event-driven .NET 10 projects. It includes:

- **NServiceBus 10** message-based architecture
- **Azure Cosmos DB** single-partition data model
- **Azure Container Apps** hosting infrastructure
- **Constitutional governance** (architectural principles)
- **Build infrastructure** (central package management, code quality rules)
- **Multi-layer architecture** template for bounded contexts

---

## 🚀 Getting Started

### Step 1: Create New Repository from Template

**On GitHub**:
1. Click **"Use this template"** button (top right)
2. Choose **"Create a new repository"**
3. Enter repository name (e.g., "RiskInsure", "FinanceHub")
4. Select visibility (public/private)
5. Click **"Create repository"**

**Via GitHub CLI**:
```bash
gh repo create YourOrg/YourProject --template YourOrg/ProjectTemplate --public
cd YourProject
```

---

### Step 2: Initialize Your Project

Choose one of three methods:

#### Option 1: Automated Script (Recommended)

```powershell
# Clone your new repository
git clone https://github.com/YourOrg/YourProject.git
cd YourProject

# Run initialization script
.\scripts\Initialize-Template.ps1 -ProjectName "YourProject"
```

**What it does**:
- Renames `ProjectTemplate.slnx` → `YourProject.slnx`
- Creates `YourProject.PublicContracts` project
- Updates all namespace references
- Updates documentation
- Optionally reinitializes git

#### Option 2: Manual Initialization

See [docs/TEMPLATE-INITIALIZATION.md](../docs/TEMPLATE-INITIALIZATION.md) for step-by-step manual instructions.

#### Option 3: AI-Assisted

```
@agent template-initialization-agent: Initialize new project
```

Follow the interactive prompts.

---

### Step 3: Build Your First Service

After initialization:

1. **Review documentation**:
   - [.specify/memory/constitution.md](../.specify/memory/constitution.md) - Architectural rules
   - [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md) - Service template

2. **Create service structure**:
   ```bash
   mkdir -p services/billing/src/{Api,Domain,Infrastructure,Endpoint.In}
   mkdir -p services/billing/test/{Api.Tests,Domain.Tests,Infrastructure.Tests,Endpoint.Tests}
   ```

3. **Start with Domain layer** (zero dependencies):
   - Define message contracts (commands/events)
   - Define domain models
   - Define business rules

4. **Add Infrastructure** (handlers, repositories):
   - Implement message handlers
   - Implement repositories
   - Add sagas for workflows

5. **Add API** (HTTP endpoints):
   - Create ASP.NET Core controllers
   - Publish commands to NServiceBus

6. **Configure Endpoint.In** (NServiceBus hosting):
   - Configure message routing
   - Configure persistence

---

## 📋 Template Checklist

After creating from template:

- [ ] Run `Initialize-Template.ps1` with your project name
- [ ] Update `CODEOWNERS` with your team/organization
- [ ] Configure branch protection rules (require reviews)
- [ ] Set up CI/CD workflows
- [ ] Configure Azure resources (Cosmos DB, Service Bus)
- [ ] Delete template-specific files (optional):
  - [ ] `scripts/Initialize-Template.ps1`
  - [ ] `.github/template-instructions.md` (this file)
  - [ ] `agents/template-initialization-agent.md`

---

## 🎯 What You Get

### Architectural Governance

✅ **Constitutional Principles** - 10 non-negotiable rules  
✅ **Project Structure Template** - Multi-layer bounded context pattern  
✅ **Coding Standards** - EditorConfig + copilot instructions

### Build Infrastructure

✅ **Directory.Build.props** - .NET 10, nullable types, warnings as errors  
✅ **Directory.Packages.props** - Central NuGet version management  
✅ **.editorconfig** - C# code style enforcement

### Security

✅ **CODEOWNERS** - Automatic PR reviewer assignment  
✅ **Dependabot** - Automated dependency updates  
✅ **Security Policy** - Vulnerability reporting process  
✅ **Enhanced .gitignore** - Prevents secret commits

### Documentation

✅ **README.md** - Project overview (customizable)  
✅ **Constitution** - Architectural principles  
✅ **Project Structure** - Service template  
✅ **Initialization Guide** - Setup instructions

---

## ❓ FAQ

**Q: Can I use this template for non-Azure projects?**  
A: Yes, but you'll need to replace Azure-specific components (Cosmos DB, Service Bus, Container Apps). Update `constitution.md` Principle IX (Technology Constraints) with your stack.

**Q: What's the difference between `PublicContracts` and `ServiceName.Domain/Contracts`?**  
A: `PublicContracts` contains **shared** events/commands used between services. `ServiceName.Domain/Contracts` contains **internal** contracts used only within that service.

**Q: Do I need to delete the template files after initialization?**  
A: Optional. The initialization script (`Initialize-Template.ps1`) and agent spec are only needed once. You can delete them after running.

**Q: Can I run the initialization script multiple times?**  
A: ⚠️ No - it's one-time use only. After renaming `ProjectTemplate.slnx`, the script will fail if run again.

**Q: How do I add a new service?**  
A: Follow the template in [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md). Create folder structure, start with Domain layer, add Infrastructure, API, and Endpoint layers.

**Q: What test coverage is required?**  
A: Domain layer 90%+, Application layer 80%+. See `constitution.md` Principle VIII.

---

## 📖 Additional Resources

- **[Template Initialization Guide](../docs/TEMPLATE-INITIALIZATION.md)** - Detailed setup instructions
- **[Constitution](../.specify/memory/constitution.md)** - Architectural principles
- **[Project Structure](../copilot-instructions/project-structure.md)** - Service template
- **[Copilot Instructions](copilot-instructions.md)** - Coding standards

---

## 🆘 Support

**Issues with template**:
- Check [TEMPLATE-INITIALIZATION.md](../docs/TEMPLATE-INITIALIZATION.md)
- Review [constitution.md](../.specify/memory/constitution.md)
- File issue in template repository (not your project repo)

**Issues with your project**:
- Review architecture documentation
- Check copilot instructions
- Run `dotnet build` and `dotnet test`

---

**End of Template Instructions**
