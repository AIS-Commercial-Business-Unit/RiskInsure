[CmdletBinding()]
param(
    [string]$RootPath = $PSScriptRoot,
    [switch]$SkipRestore,
    [switch]$ConfigurationRelease
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The 'dotnet' CLI was not found on PATH."
    exit 1
}

if (-not (Test-Path -Path $RootPath -PathType Container)) {
    Write-Error "Root path does not exist: $RootPath"
    exit 1
}

$configuration = if ($ConfigurationRelease) { 'Release' } else { 'Debug' }

Write-Host "Scanning for solutions and projects under: $RootPath"

$excludeSegmentPattern = '\\(bin|obj|node_modules|\.git|\.vs)\\'
$patterns = @('*.sln', '*.slnx', '*.csproj', '*.fsproj', '*.vbproj')

$targets = Get-ChildItem -Path $RootPath -Recurse -File -Include $patterns |
    Where-Object { $_.FullName -notmatch $excludeSegmentPattern } |
    Sort-Object FullName -Unique

if ($targets.Count -eq 0) {
    Write-Host "No solution or project files were found."
    exit 0
}

Write-Host "Found $($targets.Count) target(s)."

$failures = New-Object System.Collections.Generic.List[string]

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('clean', 'build')]
        [string]$Command,

        [Parameter(Mandatory)]
        [string]$TargetPath,

        [Parameter(Mandatory)]
        [string]$Config,

        [switch]$NoRestore
    )

    $args = @($Command, $TargetPath, '--configuration', $Config, '--nologo')

    if ($NoRestore -and $Command -eq 'build') {
        $args += '--no-restore'
    }

    Write-Host ""
    Write-Host "> dotnet $($args -join ' ')"

    & dotnet @args | Out-Host

    if ($null -eq $LASTEXITCODE) {
        return 1
    }

    return [int]$LASTEXITCODE
}

# First pass: clean everything.
foreach ($target in $targets) {
    $relative = Resolve-Path -LiteralPath $target.FullName -Relative
    Write-Host ""
    Write-Host "Cleaning: $relative"

    $exitCode = Invoke-DotNet -Command clean -TargetPath $target.FullName -Config $configuration
    if ($exitCode -ne 0) {
        $failures.Add("CLEAN FAILED: $relative") | Out-Null
    }
}

# Optional restore once at root, unless explicitly skipped.
if (-not $SkipRestore) {
    Write-Host ""
    Write-Host "Running restore at root..."
    & dotnet restore $RootPath --nologo
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("RESTORE FAILED: $RootPath") | Out-Null
    }
}

# Second pass: build everything.
foreach ($target in $targets) {
    $relative = Resolve-Path -LiteralPath $target.FullName -Relative
    Write-Host ""
    Write-Host "Building: $relative"

    $exitCode = Invoke-DotNet -Command build -TargetPath $target.FullName -Config $configuration -NoRestore:$(-not $SkipRestore)
    if ($exitCode -ne 0) {
        $failures.Add("BUILD FAILED: $relative") | Out-Null
    }
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "Completed with $($failures.Count) failure(s):"
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "Completed successfully. Clean + build passed for $($targets.Count) target(s)."
exit 0
