# Full Clean Rebuild Script
Write-Host "=== FULL CLEAN REBUILD ===" -ForegroundColor Cyan

# Step 1: Stop and remove all containers
Write-Host "`n[1/5] Stopping all containers..." -ForegroundColor Yellow
wsl docker-compose down
Start-Sleep -Seconds 2

# Step 2: Build .NET projects
Write-Host "`n[2/5] Building .NET projects..." -ForegroundColor Yellow
dotnet build services/fundstransfermgt/src/Api/Api.csproj --no-incremental
dotnet build services/policyequityandinvoicingmgt/src/Api/Api.csproj --no-incremental
dotnet build services/customerrelationshipsmgt/src/Api/Api.csproj --no-incremental
dotnet build services/policylifecyclemgt/src/Api/Api.csproj --no-incremental
dotnet build services/riskratingandunderwriting/src/Api/Api.csproj --no-incremental

# Step 3: Rebuild Docker images (no cache)
Write-Host "`n[3/5] Rebuilding Docker images..." -ForegroundColor Yellow
wsl docker-compose build --no-cache fundstransfermgt-api peimgt-api crmgt-api policylifecyclemgt-api rru-api

# Step 4: Start containers
Write-Host "`n[4/5] Starting containers..." -ForegroundColor Yellow
wsl docker-compose up -d

# Step 5: Wait for containers to be healthy
Write-Host "`n[5/5] Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Show status
Write-Host "`n=== CONTAINER STATUS ===" -ForegroundColor Cyan
wsl docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | Select-String "riskinsure"

Write-Host "`n=== REBUILD COMPLETE ===" -ForegroundColor Green
Write-Host "`nRun E2E tests with: cd test\e2e; npm test" -ForegroundColor Cyan
