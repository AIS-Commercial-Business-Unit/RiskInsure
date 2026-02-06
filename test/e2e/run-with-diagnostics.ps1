<#
.SYNOPSIS
    Run e2e tests with full diagnostic capture for Copilot analysis
.DESCRIPTION
    Runs Playwright tests and captures all context needed for debugging:
    - Test results
    - API container logs
    - Network traces
    - Screenshots on failure
.EXAMPLE
    .\run-with-diagnostics.ps1
    .\run-with-diagnostics.ps1 -Test "quote-to-policy"
#>

param(
    [string]$Test = "",  # Specific test name to run
    [switch]$UI          # Run in UI mode
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RiskInsure E2E Test Diagnostics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if APIs are running
Write-Host "[1/4] Checking API availability..." -ForegroundColor Cyan
$apiCheck = @(
    @{Name="Customer"; Port=7073}
    @{Name="Rating"; Port=7079}
    @{Name="Policy"; Port=7077}
    @{Name="Billing"; Port=7071}
    @{Name="FundsTransfer"; Port=7075}
)

$allUp = $true
foreach ($api in $apiCheck) {
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:$($api.Port)" -Method GET -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop
        Write-Host "  ✓ $($api.Name) API (port $($api.Port))" -ForegroundColor Green
    } catch {
        if ($_.Exception.Message -like "*404*") {
            Write-Host "  ✓ $($api.Name) API (port $($api.Port))" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $($api.Name) API (port $($api.Port)) - NOT RESPONDING" -ForegroundColor Red
            $allUp = $false
        }
    }
}

if (-not $allUp) {
    Write-Host ""
    Write-Host "❌ Not all APIs are running. Start services first:" -ForegroundColor Red
    Write-Host "   .\scripts\docker-start.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Run tests
Write-Host "[2/4] Running Playwright tests..." -ForegroundColor Cyan

if ($UI) {
    npm run test:ui
    exit 0
}

$testCmd = "npm test --"
if ($Test) {
    $testCmd += " -g `"$Test`""
}
$testCmd += " --reporter=list --reporter=json --reporter=html"

Write-Host "  Command: $testCmd" -ForegroundColor Gray
Write-Host ""

Invoke-Expression $testCmd
$testExitCode = $LASTEXITCODE

Write-Host ""

# Capture diagnostics if tests failed
if ($testExitCode -ne 0) {
    Write-Host "[3/4] Tests FAILED - Capturing diagnostics..." -ForegroundColor Yellow
    Write-Host ""
    
    # Create diagnostics folder
    $diagFolder = "test-results/diagnostics-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-Item -ItemType Directory -Path $diagFolder -Force | Out-Null
    
    # Capture API logs
    Write-Host "  Capturing API container logs..." -ForegroundColor Gray
    $services = @(
        "riskinsure-customer-api-1",
        "riskinsure-ratingandunderwriting-api-1",
        "riskinsure-policy-api-1",
        "riskinsure-billing-api-1",
        "riskinsure-fundstransfermgt-api-1"
    )
    
    foreach ($service in $services) {
        $logFile = "$diagFolder/$service.log"
        wsl docker logs $service --tail 200 > $logFile 2>&1
        Write-Host "    ✓ $service.log" -ForegroundColor Green
    }
    
    # Copy test results
    if (Test-Path "test-results.json") {
        Copy-Item "test-results.json" "$diagFolder/test-results.json"
        Write-Host "    ✓ test-results.json" -ForegroundColor Green
    }
    
    # Create summary for Copilot
    $summary = @"
========================================
 E2E Test Failure Diagnostics
========================================
Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

[TEST RESULTS]
Exit Code: $testExitCode
Results: See test-results.json and HTML report

[API LOGS]
Captured last 200 lines from each API container:
$(foreach ($s in $services) { "  - $s.log" })

[NEXT STEPS FOR COPILOT]
1. Review test-results.json for failure details
2. Check API logs for errors during test execution
3. Review trace files in test-results/ folders
4. Identify root cause in API code
5. Suggest fixes

[COPILOT PROMPT]
@workspace Analyze e2e test failure

The e2e tests failed. Diagnostic files captured in: $diagFolder

Please:
1. Read the test results and API logs
2. Identify which API(s) are failing
3. Review the relevant API code
4. Suggest and implement fixes

Test exit code: $testExitCode
"@
    
    $summary | Out-File "$diagFolder/COPILOT-ANALYSIS.txt" -Encoding UTF8
    
    Write-Host ""
    Write-Host "[4/4] Diagnostics captured!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📁 Location: $diagFolder" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "🤖 For Copilot Analysis:" -ForegroundColor Yellow
    Write-Host "   1. Open: $diagFolder/COPILOT-ANALYSIS.txt" -ForegroundColor Gray
    Write-Host "   2. Copy the COPILOT PROMPT section" -ForegroundColor Gray
    Write-Host "   3. Paste in Copilot chat" -ForegroundColor Gray
    Write-Host "   4. Copilot will read the logs and suggest fixes" -ForegroundColor Gray
    Write-Host ""
    
    # Open diagnostics folder
    explorer $diagFolder
    
} else {
    Write-Host "[3/4] Tests PASSED ✓" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎉 All e2e tests passed!" -ForegroundColor Green
}

Write-Host ""
Write-Host "View HTML Report: npm run test:report" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

exit $testExitCode
