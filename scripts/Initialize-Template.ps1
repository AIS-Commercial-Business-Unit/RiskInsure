<#
.SYNOPSIS
    Initializes a new project from the ProjectTemplate repository.

.DESCRIPTION
    This script transforms the generic ProjectTemplate into a specific project by:
    - Renaming the solution file
    - Creating the PublicContracts project with the new name
    - Updating all namespace references
    - Updating documentation references
    - Creating initial folder structure

.PARAMETER ProjectName
    The name of your new project (e.g., "RiskInsure", "FinanceHub", "PayrollSystem")
    - Should be PascalCase
    - Will be used for solution name, project names, and namespaces

.PARAMETER SkipGitReinit
    If specified, skips reinitializing git repository (keeps existing history)

.EXAMPLE
    .\scripts\Initialize-Template.ps1 -ProjectName "RiskInsure"
    
.EXAMPLE
    .\scripts\Initialize-Template.ps1 -ProjectName "FinanceHub" -SkipGitReinit

.NOTES
    Run this script ONCE when first using the template for a new project.
    After running, you can delete this script as it's no longer needed.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][a-zA-Z0-9]*$', ErrorMessage = "ProjectName must be PascalCase (e.g., 'MyProject')")]
    [string]$ProjectName,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipGitReinit
)

$ErrorActionPreference = 'Stop'

# Colors for output
function Write-Step { param($Message) Write-Host "▶ $Message" -ForegroundColor Cyan }
function Write-Success { param($Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Warning { param($Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "✗ $Message" -ForegroundColor Red }

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host " Project Template Initialization" -ForegroundColor Magenta
Write-Host " New Project: $ProjectName" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

# Validate we're in the repository root
if (-not (Test-Path "ProjectTemplate.slnx")) {
    Write-Error "ERROR: ProjectTemplate.slnx not found. Run this script from the repository root."
    exit 1
}

# Confirm with user
Write-Warning "This will transform the template into '$ProjectName'."
Write-Warning "This operation modifies files and cannot be easily undone."
$confirm = Read-Host "Continue? (yes/no)"
if ($confirm -ne 'yes') {
    Write-Host "Initialization cancelled." -ForegroundColor Yellow
    exit 0
}

# 1. Rename solution file
Write-Step "Renaming solution file..."
Rename-Item -Path "ProjectTemplate.slnx" -NewName "$ProjectName.slnx" -Force
Write-Success "Solution renamed to $ProjectName.slnx"

# 2. Update Directory.Build.props with default RootNamespace
Write-Step "Updating Directory.Build.props..."
$buildPropsPath = "Directory.Build.props"
$buildPropsContent = Get-Content $buildPropsPath -Raw
$buildPropsContent = $buildPropsContent -replace '</Project>', @"
  <PropertyGroup>
    <RootNamespace>$ProjectName</RootNamespace>
  </PropertyGroup>
</Project>
"@
Set-Content -Path $buildPropsPath -Value $buildPropsContent -NoNewline
Write-Success "Directory.Build.props updated with RootNamespace"

# 3. Create PublicContracts project
Write-Step "Creating PublicContracts project..."
$publicContractsDir = "platform/publiccontracts"
if (-not (Test-Path $publicContractsDir)) {
    New-Item -Path $publicContractsDir -ItemType Directory -Force | Out-Null
}

Push-Location $publicContractsDir
try {
    dotnet new classlib -n "$ProjectName.PublicContracts" -f net10.0 --force | Out-Null
    Remove-Item "Class1.cs" -ErrorAction SilentlyContinue
    
    # Create Contracts folder structure
    New-Item -Path "Contracts/Commands" -ItemType Directory -Force | Out-Null
    New-Item -Path "Contracts/Events" -ItemType Directory -Force | Out-Null
    New-Item -Path "Contracts/POCOs" -ItemType Directory -Force | Out-Null
    
    # Create README
    @"
# $ProjectName.PublicContracts

Public message contracts shared between bounded contexts.

## Purpose

This project contains **public message contracts** that are shared across service boundaries:
- Events published from one service and consumed by others
- Shared commands that cross domain boundaries
- Common DTOs used in cross-service communication

## Internal vs. Public Contracts

- **Public Contracts** (this project): Used for inter-service communication
  - Example: ``InvoiceCreated`` event from Billing consumed by Payments
  - Placed in this project and referenced by multiple services

- **Internal Contracts** (``ServiceName.Domain/Contracts``): Used within a single service
  - Example: ``ProcessPaymentInstruction`` command used only by FileIntegration
  - Placed in the service's Domain layer

## Structure

\`\`\`
Contracts/
├── Commands/        # Imperative actions (e.g., ProcessPayment, CreateInvoice)
├── Events/          # Past-tense facts (e.g., PaymentProcessed, InvoiceCreated)
└── POCOs/          # Plain data objects shared across services
\`\`\`

## Naming Conventions

- **Commands**: ``Verb`` + ``Noun`` (e.g., ``ProcessPayment``, ``CreateInvoice``)
- **Events**: ``Noun`` + ``VerbPastTense`` (e.g., ``PaymentProcessed``, ``InvoiceCreated``)
- Use C# records for immutability
- All contracts target ``net10.0``

## Standard Fields

All messages MUST include:
- ``MessageId`` (Guid): Unique identifier
- ``OccurredUtc`` (DateTimeOffset): When the event occurred
- ``IdempotencyKey`` (string): Deduplication key
- Correlation fields for distributed tracing

## Example Contract

\`\`\`csharp
namespace $ProjectName.PublicContracts.Contracts.Events;

public record InvoiceCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid InvoiceId,
    string CustomerId,
    decimal Amount,
    string IdempotencyKey
);
\`\`\`

## Versioning

This project will become a NuGet package for sharing across repositories.
Use semantic versioning for breaking changes.
"@ | Set-Content -Path "README.md"

    Write-Success "PublicContracts project created"
} finally {
    Pop-Location
}

# 4. Add PublicContracts to solution
Write-Step "Adding PublicContracts to solution..."
dotnet sln "$ProjectName.slnx" add "platform/publiccontracts/$ProjectName.PublicContracts.csproj" | Out-Null
Write-Success "PublicContracts added to solution"

# 5. Update README.md
Write-Step "Updating README.md..."
$readmePath = "README.md"
$readmeContent = @"
# $ProjectName

Event-driven .NET 10 monorepo using NServiceBus, Azure Cosmos DB, and Azure Container Apps.

## 🏗️ Architecture

This repository implements an **event-driven architecture** with:
- **NServiceBus 9.x** for message-based integration via RabbitMQ transport
- **Azure Cosmos DB** for single-partition NoSQL persistence
- **Azure Container Apps** for hosting NServiceBus endpoints with KEDA scaling
- **Azure Logic Apps Standard** for orchestration workflows

## 📁 Repository Structure

\`\`\`
$ProjectName/
├── platform/                    # Cross-cutting concerns
│   ├── publiccontracts/         # Shared message contracts (events/commands)
│   ├── ui/                      # Shared UI components
│   └── infra/                   # Infrastructure templates (Bicep, Terraform)
├── services/                    # Business-specific bounded contexts
│   ├── billing/                 # Billing domain service
│   ├── payments/                # Payments domain service
│   └── [your-service]/          # Add your services here
├── copilot-instructions/        # Architectural governance
│   ├── constitution.md          # Non-negotiable architectural principles
│   └── project-structure.md     # Bounded context template
└── scripts/                     # Automation scripts
\`\`\`

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Git](https://git-scm.com/)

### First-Time Setup

1. **Clone the repository**
   \`\`\`bash
   git clone https://github.com/your-org/$ProjectName.git
   cd $ProjectName
   \`\`\`

2. **Restore dependencies**
   \`\`\`bash
   dotnet restore
   \`\`\`

3. **Build the solution**
   \`\`\`bash
   dotnet build
   \`\`\`

4. **Run tests**
   \`\`\`bash
   dotnet test
   \`\`\`

### Creating Your First Service

See [copilot-instructions/project-structure.md](copilot-instructions/project-structure.md) for the bounded context template.

**Quick steps**:
1. Create folder structure: \`services/yourservice/src/{Api,Domain,Infrastructure,Endpoint.In}\`
2. Start with Domain layer (contracts, models, interfaces)
3. Add Infrastructure layer (handlers, repositories)
4. Add API layer (HTTP endpoints)
5. Configure Endpoint.In (NServiceBus hosting)
6. Add all projects to solution: \`dotnet sln add <path-to-csproj>\`

## 📖 Documentation

- **[Constitution](.specify/memory/constitution.md)** - Non-negotiable architectural rules
- **[Project Structure](copilot-instructions/project-structure.md)** - Bounded context template
- **[Copilot Instructions](.github/copilot-instructions.md)** - Coding assistant rules
- **[Template Initialization](docs/TEMPLATE-INITIALIZATION.md)** - How this template was initialized

## 🧪 Testing Strategy

- **Domain Layer**: 90%+ coverage (pure business logic)
- **Application Layer**: 80%+ coverage (services, handlers)
- **Infrastructure**: Integration tests with Cosmos DB emulator
- **Framework**: xUnit with AAA pattern

## 🔐 Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## 📋 Contributing

1. Review [constitution.md](.specify/memory/constitution.md) principles
2. Follow [project-structure.md](copilot-instructions/project-structure.md) template
3. Ensure test coverage meets thresholds
4. All PRs require review from @your-org/contributors

## 📄 License

[Your License Here]
"@
Set-Content -Path $readmePath -Value $readmeContent
Write-Success "README.md updated"

# 6. Update copilot-instructions.md references
Write-Step "Updating copilot-instructions..."
$copilotInstructionsPath = ".github/copilot-instructions.md"
if (Test-Path $copilotInstructionsPath) {
    $copilotContent = Get-Content $copilotInstructionsPath -Raw
    $copilotContent = $copilotContent -replace 'RiskInsure', $ProjectName
    Set-Content -Path $copilotInstructionsPath -Value $copilotContent -NoNewline
    Write-Success "Copilot instructions updated"
}

# 7. Create initial documentation
Write-Step "Creating initialization documentation..."
$initDocPath = "docs/TEMPLATE-INITIALIZATION.md"
@"
# Template Initialization Record

**Project Name**: $ProjectName  
**Initialized**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  
**Template Version**: 1.0.0

## Initialization Steps Completed

- ✅ Solution file renamed: ProjectTemplate.slnx → $ProjectName.slnx
- ✅ RootNamespace set to: $ProjectName
- ✅ PublicContracts project created: $ProjectName.PublicContracts
- ✅ PublicContracts added to solution
- ✅ README.md updated with project name
- ✅ Copilot instructions updated with project name

## Next Steps

1. **Create your first service**
   - Follow [project-structure.md](../copilot-instructions/project-structure.md)
   - Start with Domain layer (contracts, models)
   - Add Infrastructure, API, and Endpoint layers

2. **Configure local development**
   - Set up Cosmos DB emulator
   - Set up Azure Service Bus (cloud) or RabbitMQ (local container or managed broker)
   - Copy appsettings.Development.json.template files

3. **Define your domain**
   - Document domain terminology in service-specific standards
   - Design message contracts (commands/events)
   - Implement domain models

4. **Set up CI/CD**
   - Configure GitHub Actions workflows
    - Set up Azure resources (Cosmos DB, Service Bus or RabbitMQ broker, Container Apps)

## Template Cleanup (Optional)

You can now delete these template-specific files:
- \`scripts/Initialize-Template.ps1\` (this initialization script)
- \`.github/template-instructions.md\` (template usage guide)

## Reference Documentation

- **[Constitution](../.specify/memory/constitution.md)** - Architectural principles
- **[Project Structure](../copilot-instructions/project-structure.md)** - Service template
- **[Copilot Instructions](../.github/copilot-instructions.md)** - Coding standards
"@ | Set-Content -Path $initDocPath
Write-Success "Initialization documentation created"

# 8. Optionally reinitialize git
if (-not $SkipGitReinit) {
    Write-Step "Reinitializing git repository..."
    Write-Warning "This will remove all template commit history."
    $confirmGit = Read-Host "Reinitialize git? (yes/no)"
    if ($confirmGit -eq 'yes') {
        Remove-Item -Path ".git" -Recurse -Force -ErrorAction SilentlyContinue
        git init
        git add .
        git commit -m "Initial commit from template - $ProjectName"
        Write-Success "Git repository reinitialized"
    } else {
        Write-Warning "Skipped git reinitialization"
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host " ✓ Initialization Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Magenta

Write-Host "Project Name:     " -NoNewline; Write-Host $ProjectName -ForegroundColor Cyan
Write-Host "Solution File:    " -NoNewline; Write-Host "$ProjectName.slnx" -ForegroundColor Cyan
Write-Host "PublicContracts:  " -NoNewline; Write-Host "$ProjectName.PublicContracts" -ForegroundColor Cyan

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Review docs/TEMPLATE-INITIALIZATION.md"
Write-Host "  2. Create your first service (see copilot-instructions/project-structure.md)"
Write-Host "  3. Run: dotnet build"
Write-Host "  4. Run: dotnet test`n"

Write-Host "Documentation:" -ForegroundColor Yellow
Write-Host "  - Constitution:      .specify/memory/constitution.md"
Write-Host "  - Project Structure: copilot-instructions/project-structure.md"
Write-Host "  - Copilot Rules:     .github/copilot-instructions.md`n"
