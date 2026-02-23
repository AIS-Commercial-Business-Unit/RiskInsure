#!/usr/bin/env pwsh
# Setup implementation plan for a feature

[CmdletBinding()]
param(
    [switch]$Json,
    [string]$SpecPath,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

# Show help if requested
if ($Help) {
    Write-Output "Usage: ./setup-plan.ps1 [-Json] [-SpecPath <path-to-spec>] [-Help]"
    Write-Output "  -Json     Output results in JSON format"
    Write-Output "  -SpecPath Explicit spec path (e.g., services/nsb.sales/specs/001-sales-ordering/spec.md)"
    Write-Output "  -Help     Show this help message"
    exit 0
}

# Load common functions
. "$PSScriptRoot/common.ps1"

# Get all paths and variables from common functions
$paths = Get-FeaturePathsEnv -SpecPath $SpecPath

# Check if we're on a proper feature branch (only for git repos)
if (-not $SpecPath) {
    if (-not (Test-FeatureBranch -Branch $paths.CURRENT_BRANCH -HasGit $paths.HAS_GIT)) {
        exit 1
    }
}

# Ensure the feature directory exists
New-Item -ItemType Directory -Path $paths.FEATURE_DIR -Force | Out-Null

function Get-RelativePathFromBase {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseResolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $BasePath).Path)
    $targetResolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $TargetPath).Path)
    $relativePath = [System.IO.Path]::GetRelativePath($baseResolved, $targetResolved)

    return $relativePath.Replace('\', '/')
}

function Get-RepoRelativePath {
    param(
        [string]$RepoRoot,
        [string]$TargetPath
    )

    return Get-RelativePathFromBase -BasePath $RepoRoot -TargetPath $TargetPath
}

# Copy plan template if it exists, otherwise note it or create empty file
$template = Join-Path $paths.REPO_ROOT '.specify/templates/plan-template.md'
if (Test-Path $template) { 
    Copy-Item $template $paths.IMPL_PLAN -Force
    Write-Output "Copied plan template to $($paths.IMPL_PLAN)"
} else {
    Write-Warning "Plan template not found at $template"
    # Create a basic plan file if template doesn't exist
    New-Item -ItemType File -Path $paths.IMPL_PLAN -Force | Out-Null
}

function Get-PersistenceDecisionFromSpec {
    param(
        [string]$SpecPath
    )

    if (-not (Test-Path $SpecPath)) {
        return $null
    }

    $lines = Get-Content -LiteralPath $SpecPath -Encoding utf8
    $decision = $null
    $rationale = $null

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*-\s*\[x\]\s*Azure Cosmos DB') {
            $decision = 'Cosmos DB'
        } elseif ($lines[$i] -match '^\s*-\s*\[x\]\s*PostgreSQL') {
            $decision = 'PostgreSQL'
        }

        if ($decision) {
            for ($j = $i + 1; $j -lt [Math]::Min($i + 6, $lines.Count); $j++) {
                if ($lines[$j] -match '^\s*-\s*Rationale:\s*(.+)$') {
                    $rationale = $Matches[1].Trim()
                    break
                }
            }
            break
        }
    }

    if (-not $decision) {
        return $null
    }

    return [PSCustomObject]@{
        Decision = $decision
        Rationale = $rationale
    }
}

$persistence = Get-PersistenceDecisionFromSpec -SpecPath $paths.FEATURE_SPEC
if (Test-Path $paths.IMPL_PLAN) {
    $planContent = Get-Content -LiteralPath $paths.IMPL_PLAN -Raw -Encoding utf8

    $featureSpecRel = Get-RepoRelativePath -RepoRoot $paths.REPO_ROOT -TargetPath $paths.FEATURE_SPEC
    $featureDirRel = Get-RepoRelativePath -RepoRoot $paths.REPO_ROOT -TargetPath $paths.FEATURE_DIR

    $featureDirRelNormalized = $featureDirRel.Replace('\', '/')

    $serviceRootRel = $null
    if ($featureDirRelNormalized -match '^(.*?)/specs/[^/]+$') {
        $serviceRootRel = $Matches[1]
    }
    if (-not $serviceRootRel) {
        $serviceRootRel = $featureDirRelNormalized
    }

    $constitutionPath = Join-Path $paths.REPO_ROOT '.specify/memory/constitution.md'
    $constitutionRel = Get-RelativePathFromBase -BasePath $paths.FEATURE_DIR -TargetPath $constitutionPath

    $planContent = $planContent.Replace('[FEATURE_SPEC_REL]', $featureSpecRel)
    $planContent = $planContent.Replace('[FEATURE_DIR_REL]', $featureDirRelNormalized)
    $planContent = $planContent.Replace('[SERVICE_ROOT_REL]', $serviceRootRel)
    $planContent = $planContent.Replace('[CONSTITUTION_REL]', $constitutionRel)

    if ($persistence) {
    $planContent = $planContent -replace '\*\*DECISION\*\*:\s*\[.*?\]', "**DECISION**: $($persistence.Decision)"
    if ($persistence.Rationale) {
        $planContent = $planContent -replace '\*\*Rationale\*\*:\s*\[.*?\]', "**Rationale**: $($persistence.Rationale)"
    }
    }

    Set-Content -LiteralPath $paths.IMPL_PLAN -Value $planContent -Encoding utf8

    if ($planContent -match 'services/<domain>|\[FEATURE_SPEC_REL\]|\[FEATURE_DIR_REL\]|\[SERVICE_ROOT_REL\]|\[CONSTITUTION_REL\]') {
        Write-Warning "Plan contains unresolved path placeholders. Review $($paths.IMPL_PLAN)."
    }
}

# Output results
if ($Json) {
    $result = [PSCustomObject]@{ 
        FEATURE_SPEC = $paths.FEATURE_SPEC
        IMPL_PLAN = $paths.IMPL_PLAN
        SPECS_DIR = $paths.FEATURE_DIR
        BRANCH = $paths.CURRENT_BRANCH
        HAS_GIT = $paths.HAS_GIT
    }
    $result | ConvertTo-Json -Compress
} else {
    Write-Output "FEATURE_SPEC: $($paths.FEATURE_SPEC)"
    Write-Output "IMPL_PLAN: $($paths.IMPL_PLAN)"
    Write-Output "SPECS_DIR: $($paths.FEATURE_DIR)"
    Write-Output "BRANCH: $($paths.CURRENT_BRANCH)"
    Write-Output "HAS_GIT: $($paths.HAS_GIT)"
}
