#!/usr/bin/env pwsh
# Common PowerShell functions analogous to common.sh

function Get-RepoRoot {
    try {
        $result = git rev-parse --show-toplevel 2>$null
        if ($LASTEXITCODE -eq 0) {
            return $result
        }
    } catch {
        # Git command failed
    }
    
    # Fall back to script location for non-git repos
    return (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
}

function Get-CurrentBranch {
    # First check if SPECIFY_FEATURE environment variable is set
    if ($env:SPECIFY_FEATURE) {
        return $env:SPECIFY_FEATURE
    }
    
    # Then check git if available
    try {
        $result = git rev-parse --abbrev-ref HEAD 2>$null
        if ($LASTEXITCODE -eq 0) {
            return $result
        }
    } catch {
        # Git command failed
    }
    
    # For non-git repos, try to find the latest feature directory
    $repoRoot = Get-RepoRoot
    $specsDir = Get-SpecsRoot -RepoRoot $repoRoot
    
    if (Test-Path $specsDir) {
        $latestFeature = ""
        $highest = 0
        
        Get-ChildItem -Path $specsDir -Directory | ForEach-Object {
            if ($_.Name -match '^(\d{3})-') {
                $num = [int]$matches[1]
                if ($num -gt $highest) {
                    $highest = $num
                    $latestFeature = $_.Name
                }
            }
        }
        
        if ($latestFeature) {
            return $latestFeature
        }
    }
    
    # Final fallback
    return "main"
}

function Test-HasGit {
    try {
        git rev-parse --show-toplevel 2>$null | Out-Null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
}

function Test-FeatureBranch {
    param(
        [string]$Branch,
        [bool]$HasGit = $true
    )
    
    # For non-git repos, we can't enforce branch naming but still provide output
    if (-not $HasGit) {
        Write-Warning "[specify] Warning: Git repository not detected; skipped branch validation"
        return $true
    }
    
    if ($Branch -notmatch '^[0-9]{3}-') {
        Write-Output "ERROR: Not on a feature branch. Current branch: $Branch"
        Write-Output "Feature branches should be named like: 001-feature-name"
        return $false
    }
    return $true
}

function Get-SpecsRoot {
    param([string]$RepoRoot)

    if ($env:SPECIFY_SPECS_ROOT) {
        if ([System.IO.Path]::IsPathRooted($env:SPECIFY_SPECS_ROOT)) {
            return (Resolve-Path $env:SPECIFY_SPECS_ROOT).Path
        }
        return (Resolve-Path (Join-Path $RepoRoot $env:SPECIFY_SPECS_ROOT)).Path
    }

    if ($env:SPECIFY_SERVICE) {
        if (Test-Path $env:SPECIFY_SERVICE) {
            return (Resolve-Path (Join-Path $env:SPECIFY_SERVICE 'specs')).Path
        }
        return (Resolve-Path (Join-Path $RepoRoot (Join-Path 'services' (Join-Path $env:SPECIFY_SERVICE 'specs')))).Path
    }

    return (Join-Path $RepoRoot 'specs')
}

function Get-FeatureDir {
    param([string]$RepoRoot, [string]$Branch)
    $specsRoot = Get-SpecsRoot -RepoRoot $RepoRoot
    Join-Path $specsRoot $Branch
}

function Resolve-SpecPath {
    param(
        [string]$RepoRoot,
        [string]$SpecPath
    )

    if ([string]::IsNullOrWhiteSpace($SpecPath)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($SpecPath)) {
        return (Resolve-Path $SpecPath).Path
    }

    return (Resolve-Path (Join-Path $RepoRoot $SpecPath)).Path
}

function Get-FeaturePathsEnv {
    param(
        [string]$SpecPath
    )

    $repoRoot = Get-RepoRoot
    $currentBranch = Get-CurrentBranch
    $hasGit = Test-HasGit

    $resolvedSpecPath = Resolve-SpecPath -RepoRoot $repoRoot -SpecPath $SpecPath

    if ($resolvedSpecPath) {
        $featureDir = Split-Path -Parent $resolvedSpecPath
        $featureSpec = $resolvedSpecPath
    } else {
        $featureDir = Get-FeatureDir -RepoRoot $repoRoot -Branch $currentBranch
        $featureSpec = Join-Path $featureDir 'spec.md'
    }

    [PSCustomObject]@{
        REPO_ROOT      = $repoRoot
        CURRENT_BRANCH = $currentBranch
        HAS_GIT        = $hasGit
        FEATURE_DIR    = $featureDir
        FEATURE_SPEC   = $featureSpec
        IMPL_PLAN      = Join-Path $featureDir 'plan.md'
        TASKS          = Join-Path $featureDir 'tasks.md'
        RESEARCH       = Join-Path $featureDir 'research.md'
        DATA_MODEL     = Join-Path $featureDir 'data-model.md'
        QUICKSTART     = Join-Path $featureDir 'quickstart.md'
        CONTRACTS_DIR  = Join-Path $featureDir 'contracts'
    }
}

function Test-FileExists {
    param([string]$Path, [string]$Description)
    if (Test-Path -Path $Path -PathType Leaf) {
        Write-Output "  ✓ $Description"
        return $true
    } else {
        Write-Output "  ✗ $Description"
        return $false
    }
}

function Test-DirHasFiles {
    param([string]$Path, [string]$Description)
    if ((Test-Path -Path $Path -PathType Container) -and (Get-ChildItem -Path $Path -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer } | Select-Object -First 1)) {
        Write-Output "  ✓ $Description"
        return $true
    } else {
        Write-Output "  ✗ $Description"
        return $false
    }
}

