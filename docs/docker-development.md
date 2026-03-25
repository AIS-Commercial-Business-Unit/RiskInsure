# Docker Compose Development Guide

**Run all 10 RiskInsure services (5 domains × API + Endpoint.In) with a single command using Rancher Desktop.**

---

## 🚀 Quick Start

### 1. Install Rancher Desktop

- Download from https://rancherdesktop.io/
- **Container Engine**: Select **"dockerd (moby)"** (not containerd)
- Disable Kubernetes if not needed (saves resources)
- Restart VS Code after installation to pick up PATH changes

### 2. One-Time Setup

```powershell
# Navigate to repository root
cd C:\Dev\AIS-Commercial-Business-Unit\RiskInsure

# Copy environment template
Copy-Item .env.example .env

# Edit .env with your connection strings
notepad .env
```

**Required in `.env`**:
- `COSMOSDB_CONNECTION_STRING=AccountEndpoint=https://...;AccountKey=...;`
- `SERVICEBUS_CONNECTION_STRING=Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...;`
- `RABBITMQ_CONNECTION_STRING=host=rabbitmq;username=guest;password=guest`

**⚠️ Common Mistake**: Don't duplicate `Endpoint=` in Service Bus connection string.
**⚠️ Common Mistake**: Use RabbitMQ format (`host=...;username=...;password=...`) instead of legacy endpoint-style connection string formats.

### 3. Configure WSL DNS (One-Time)

```powershell
# Fix DNS resolution in WSL
wsl sudo sh -c "echo 'nameserver 8.8.8.8' > /etc/resolv.conf"

# Configure Docker DNS
wsl sudo mkdir -p /etc/docker
wsl sudo sh -c 'cat > /etc/docker/daemon.json << EOF
{
  "dns": ["8.8.8.8", "8.8.4.4", "1.1.1.1"]
}
EOF'
```

Restart Rancher Desktop after DNS configuration.

### 4. Start All Services

```powershell
.\scripts\docker-start.ps1
```

**That's it!** All 10 services are now running.

---

## 📋 Common Commands

| Task | Command |
|------|---------|
| **Start all services** | `.\scripts\docker-start.ps1` |
| **Stop all services** | `.\scripts\docker-stop.ps1` |
| **Rebuild after code changes** | `.\scripts\docker-rebuild.ps1` |
| **Run smoke test** | `.\scripts\smoke-test.ps1` |
| **View all logs** | `.\scripts\docker-logs.ps1` |
| **View specific service logs** | `.\scripts\docker-logs.ps1 -Service crmgt-api` |
| **Check service status** | `.\scripts\docker-status.ps1` |
| **Force clean rebuild** | `.\scripts\docker-rebuild.ps1 -CleanBuild` |
| **Stop and remove containers** | `.\scripts\docker-stop.ps1 -Remove` |

**Note**: With Rancher Desktop (WSL2), scripts automatically use `wsl docker` commands.

---

## 🌐 Service Endpoints

| Domain | API Port | Endpoint.In | Purpose |
|--------|----------|-------------|---------|
| **Funds Transfer** | [localhost:7075](http://localhost:7075) | Running in background | Payment processing |
| **Policy Equity & Invoicing Mgt** | [localhost:7081](http://localhost:7081) | Running in background | Billing and invoicing operations |
| **Customer Relationships Mgt** | [localhost:7083](http://localhost:7083) | Running in background | Customer relationship management |
| **Policy Lifecycle Mgt** | [localhost:7085](http://localhost:7085) | Running in background | Policy lifecycle management |
| **Risk Rating & Underwriting** | [localhost:7087](http://localhost:7087) | Running in background | Quote and underwriting |

All APIs support Scalar documentation at `/scalar/v1` (e.g., http://localhost:7081/scalar/v1)

---

## 💡 Rancher Desktop Integration

### Viewing All Services

Rancher Desktop integrates with Docker CLI - use standard Docker commands:

```powershell
# View running containers
wsl docker ps

# View all containers including stopped
wsl docker ps -a

# View logs
wsl docker logs riskinsure-crmgt-api-1

# Check resource usage
wsl docker stats
```

**Rancher Desktop UI** shows:
- Container status
- Resource usage (CPU, memory)
- Kubernetes integration (if enabled)

---

## 🔧 Advanced Usage

### Rebuild Specific Service

```powershell
# Rebuild just one service
wsl docker-compose up -d --build crmgt-api

# Or use the script (rebuilds all)
.\scripts\docker-rebuild.ps1
```

### View Logs with Filtering

```powershell
# Last 100 lines
wsl docker-compose logs --tail=100

# Specific service
wsl docker-compose logs --tail=50 policylifecyclemgt-api

# Follow logs in real-time
wsl docker-compose logs -f crmgt-api
```

### Restart Individual Service

```powershell
# Restart without rebuilding (e.g., after .env change)
wsl docker-compose restart crmgt-api

# Restart all APIs
wsl docker-compose restart crmgt-api fundstransfermgt-api policylifecyclemgt-api peimgt-api rru-api
```

---

## 🐛 Troubleshooting

### Docker Command Not Found

**Symptom**: `'docker' is not recognized`

**Solutions**:
1. **Restart VS Code** after installing Rancher Desktop
2. Open a new PowerShell terminal (old terminals won't have updated PATH)
3. Verify: `wsl docker version` should show Client and Server

### Permission Denied / Cannot Connect to Docker API

**Symptom**: `permission denied while trying to connect to the docker API at npipe:////./pipe/docker_engine`

**Root Cause**: Rancher Desktop uses WSL2, not Windows named pipes.

**Solution**: Scripts automatically use `wsl docker` - no action needed. If running docker commands directly, use `wsl docker` prefix.

### DNS Resolution Failures

**Symptom**: `failed to resolve source metadata for mcr.microsoft.com/dotnet/sdk:10.0`

**Solution**:
```powershell
# Configure WSL DNS
wsl sudo sh -c "echo 'nameserver 8.8.8.8' > /etc/resolv.conf"
wsl sudo sh -c "echo 'nameserver 1.1.1.1' >> /etc/resolv.conf"

# Configure Docker DNS
wsl sudo mkdir -p /etc/docker
wsl sudo sh -c 'cat > /etc/docker/daemon.json << EOF
{
  "dns": ["8.8.8.8", "8.8.4.4", "1.1.1.1"]
}
EOF'
```

Restart Rancher Desktop after changes.

### Invalid RabbitMQ Connection String

**Symptom**: Transport fails during startup or cannot connect to broker.

**Solution**: Check `.env` for RabbitMQ key/value format:
```bash
# ❌ Wrong
RABBITMQ_CONNECTION_STRING=amqp://...

# ✅ Correct
RABBITMQ_CONNECTION_STRING=host=rabbitmq;username=guest;password=guest
```

### Container Exited (139) - Segmentation Fault

**Symptom**: Container crashes with exit code 139

**Root Cause**: Usually missing dependency in DI container (e.g., `Microsoft.Azure.Cosmos.Container`)

**Solution**:
1. Check logs: `wsl docker logs <container-name> --tail 50`
2. Look for `Unable to resolve service for type` errors
3. Compare with working service's `Program.cs`
4. Add missing registrations

### Rancher Desktop Won't Start / Port 6120 in Use

**Symptom**: `Error: listen EADDRINUSE: address already in use 127.0.0.1:6120`

**Solution**:
```powershell
# Kill all Rancher Desktop processes
Get-Process | Where-Object ProcessName -like "*Rancher*" | Stop-Process -Force

# Wait 5 seconds
Start-Sleep -Seconds 5

# Restart Rancher Desktop
Start-Process "C:\Program Files\Rancher Desktop\Rancher Desktop.exe"
```

### Services Won't Start After Code Changes

**Always rebuild** after changing code:
```powershell
.\scripts\docker-rebuild.ps1
```

**When to rebuild**:
- ✅ Code changes (.cs files)
- ✅ appsettings.json changes
- ✅ NuGet package changes
- ❌ .env changes (just restart: `wsl docker-compose restart`)

---

## 🏗️ How It Works

### Rancher Desktop Architecture

```
Windows (PowerShell)
  ↓
  .\scripts\docker-start.ps1
  ↓
  wsl docker-compose up -d
  ↓
WSL2 (Linux)
  ↓
  Docker Engine (dockerd/moby)
  ↓
  Containers (crmgt-api, peimgt-api, etc.)
```

**Key Points**:
- Docker runs **inside WSL2**, not natively on Windows
- All docker commands prefixed with `wsl` in scripts
- Connection strings passed via environment variables
- DNS configured at both WSL and Docker levels

### Service Architecture

```
docker-compose.yml
│
├── peimgt-api (7081) ───────┐
├── peimgt-endpoint ─────────┤
│                             ├──> Shared Network
├── crmgt-api (7083) ────────┤    (riskinsure)
├── crmgt-endpoint ──────────┤
│                             │
├── fundstransfermgt-api ────┤
├── fundstransfermgt-endpoint┤
│                             │
├── policylifecyclemgt-api ──┤
├── policylifecyclemgt-endpoint
│                             │
├── rru-api (7087) ──────────┤
└── rru-endpoint ────────────┘
```

### Build Process

1. **Build Stage** (mcr.microsoft.com/dotnet/sdk:10.0):
   - Copies solution and project files
   - Restores NuGet packages
   - Builds and publishes to `/app/publish`

2. **Runtime Stage** (mcr.microsoft.com/dotnet/aspnet:10.0):
   - Copies published output
   - Runs application (smaller image)

### Environment Variables

Passed from `.env` → `docker-compose.yml` → containers:
- `COSMOSDB_CONNECTION_STRING`
- `SERVICEBUS_CONNECTION_STRING`
- `RABBITMQ_CONNECTION_STRING`
- `ASPNETCORE_ENVIRONMENT=Development`
- `ASPNETCORE_URLS=http://+:8080` (internal port, mapped to 707X)

---

## 📦 What Gets Included in Docker Images

### Included:
- ✅ Compiled .NET assemblies
- ✅ Dependencies (NuGet packages)
- ✅ `appsettings.json` (base configuration)

### Excluded (via .dockerignore):
- ❌ Source code (`.cs` files - already compiled)
- ❌ `bin/` and `obj/` folders
- ❌ Test projects
- ❌ Documentation
- ❌ `.git` folder
- ❌ `node_modules/`
- ❌ `appsettings.Development.json` (uses environment variables instead)

---

## 🔄 Development Workflow

### Typical Day

```powershell
# Morning: Start all services
.\scripts\docker-start.ps1

# Make code changes in Visual Studio...

# Rebuild changed service
.\scripts\docker-rebuild.ps1

# Run E2E tests
cd test/e2e
npm test

# End of day: Stop all
.\scripts\docker-stop.ps1
```

### When to Rebuild

| Change Type | Rebuild Required? | Command |
|-------------|-------------------|---------|
| Code change | ✅ Yes | `.\scripts\docker-rebuild.ps1` |
| `appsettings.json` change | ✅ Yes | `.\scripts\docker-rebuild.ps1` |
| `.env` change | ❌ No | `docker-compose restart` |
| Added NuGet package | ✅ Yes | `.\scripts\docker-rebuild.ps1 -CleanBuild` |
| Changed `Dockerfile` | ✅ Yes | `.\scripts\docker-rebuild.ps1 -CleanBuild` |

---

## 🧪 Integration with E2E Tests

E2E tests in `/test/e2e/` automatically use the running Docker services:

```powershell
# Terminal 1: Start services
.\scripts\docker-start.ps1

# Terminal 2: Run E2E tests
cd test/e2e
npm test
```

No configuration changes needed - tests use `localhost:707X` by default.

---

## 🚀 Performance Tips

### Speed Up Builds

1. **Layer caching**: Docker caches unchanged layers
   - Don't rebuild unless code changed
   - Use `.\scripts\docker-rebuild.ps1` (not `-CleanBuild`)

2. **Multi-stage builds**: Already optimized in Dockerfiles
   - Build stage: Heavy SDK image
   - Runtime stage: Lightweight runtime image

3. **Parallel builds**: Docker Compose builds services in parallel automatically

### Resource Usage

**Typical resource usage per service**:
- API: ~100-200 MB RAM
- Endpoint.In: ~100-150 MB RAM
- Total: ~1.5-2 GB RAM for all 10 services

**Adjust Rancher Desktop resources**:
- Preferences → Virtual Machine → Memory: 8 GB recommended
- Preferences → Virtual Machine → CPUs: 4+ cores recommended

**Note**: Rancher Desktop runs Docker in WSL2, which uses dynamic memory allocation.

---

## 📊 Monitoring & Debugging

### Quick Health Check

```powershell
# Run smoke test (fastest)
.\scripts\smoke-test.ps1

# Or view status
.\scripts\docker-status.ps1
```

### View Resource Usage

```powershell
# Real-time stats
wsl docker stats

# Specific container
wsl docker stats riskinsure-crmgt-api-1
```

### Debug Container Issues

```powershell
# View logs
wsl docker logs riskinsure-crmgt-api-1 --tail 50

# Follow logs in real-time
wsl docker logs -f riskinsure-crmgt-api-1

# Execute command inside container
wsl docker exec -it riskinsure-crmgt-api-1 /bin/bash

# View environment variables
wsl docker exec riskinsure-crmgt-api-1 env

# Inspect container
wsl docker inspect riskinsure-crmgt-api-1
```

---

## 🔐 Security Considerations

### Connection Strings

- ✅ **DO**: Store in `.env` file (gitignored)
- ✅ **DO**: Use Azure Key Vault in production
- ❌ **DON'T**: Commit `.env` to git
- ❌ **DON'T**: Hardcode in `docker-compose.yml`

### Container Security

- ✅ Multi-stage builds (minimal runtime image)
- ✅ Non-root user (if configured in Dockerfile)
- ✅ Latest .NET runtime (security patches)
- ❌ Don't expose unnecessary ports

---

## 🧪 Smoke Testing

After starting services, verify everything is running:

```powershell
.\scripts\smoke-test.ps1
```

**What it checks**:
- ✅ Docker daemon running
- ✅ All 10 containers operational
- ✅ API endpoints responding (ports 7071, 7073, 7075, 7077, 7079)
- ✅ .env configuration valid
- ✅ No crashed or restarting containers

**Output**: Pass/Fail with specific issues and remediation steps.

**Execution time**: ~4 seconds

---

## 🆚 Why Docker Compose?

| Aspect | Individual Processes | Docker Compose |
|--------|---------------------|----------------|
| **Start time** | 5-10 minutes | 30 seconds |
| **Commands** | 14 terminals | 1 command |
| **Consistency** | Environment varies | Identical across team |
| **Cleanup** | Kill each process | `.\scripts\docker-stop.ps1` |
| **CI/CD ready** | No | Yes ✅ |
| **Smoke testing** | Manual | `.\scripts\smoke-test.ps1` |

---

## 🎯 Next Steps

1. ✅ **Try it now**: `.\scripts\docker-start.ps1`
2. 🧪 **Run smoke test**: `.\scripts\smoke-test.ps1`
3. 🔍 **Check logs**: `wsl docker logs riskinsure-crmgt-api-1`
4. 🔧 **Configure DNS**: Follow troubleshooting if DNS issues occur
5. 📚 **Share this guide** with your team

**Pro tip**: Run smoke test before and after making changes to catch issues early.

---

## 📝 Quick Reference Card

**Print this out and tape to your monitor! 📌**

```
═══════════════════════════════════════
    RiskInsure Docker Quick Commands
═══════════════════════════════════════

START:      .\scripts\docker-start.ps1
STOP:       .\scripts\docker-stop.ps1
REBUILD:    .\scripts\docker-rebuild.ps1
SMOKE TEST: .\scripts\smoke-test.ps1
LOGS:       .\scripts\docker-logs.ps1
STATUS:     .\scripts\docker-status.ps1

Manual Commands:
  wsl docker ps              # List running
  wsl docker logs <name>     # View logs
  wsl docker-compose restart # Restart all

═══════════════════════════════════════
    API Endpoints
═══════════════════════════════════════

Billing:    http://localhost:7071
Customer:   http://localhost:7073
Funds:      http://localhost:7075
Policy:     http://localhost:7077
Rating:     http://localhost:7079

═══════════════════════════════════════
    Troubleshooting Quick Fixes
═══════════════════════════════════════

Docker not found?
  → Restart VS Code

DNS issues?
  → wsl sudo sh -c "echo 'nameserver 8.8.8.8' > /etc/resolv.conf"

Container crashed?
  → wsl docker logs <container-name>
  → Check Program.cs for missing DI registrations

Connection string error?
  → Check .env for duplicate "Endpoint="

═══════════════════════════════════════
```

---

**Version**: 2.0.0 (Rancher Desktop)  
**Last Updated**: February 6, 2026  
**Status**: ✅ **PRODUCTION-READY**
