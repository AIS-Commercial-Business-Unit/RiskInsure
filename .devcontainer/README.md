# GitHub Codespaces Setup for RiskInsure

This directory contains the configuration for developing RiskInsure in GitHub Codespaces with **full local emulation** - no Azure resources required!

## What's Included

- ✅ .NET 10 SDK
- ✅ Docker-in-Docker (for running all 10 service containers)
- ✅ RabbitMQ broker (local message queuing)
- ✅ Cosmos DB Emulator (local database)
- ✅ Node.js 20 (for Playwright E2E tests)
- ✅ GitHub Copilot & Copilot Chat
- ✅ Azure CLI

## Quick Start

### 1. Open in Codespace

Click "Code" → "Codespaces" → "Create codespace on main"

The devcontainer will automatically:
- Install all dependencies
- Wait for emulators to start
- Restore NuGet packages
- Install Playwright browsers

This takes **~5-10 minutes** on first launch.

### 2. Start All Services

```bash
docker-compose up -d
```

This starts:
- 5 API containers (Customer, Rating, Policy, Billing, FundsTransferMgt)
- 5 NServiceBus Endpoint containers
- RabbitMQ broker (already running)
- Cosmos DB Emulator (already running)

### 3. View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker logs riskinsure-policy-api-1 -f
```

### 4. Run Tests

```bash
cd test/e2e
npm test                    # Run all tests
npm run test:ui            # Interactive Playwright UI
npm run test:debug         # Debug mode
```

### 5. Stop Services

```bash
docker-compose down         # Stop containers
docker-compose down -v      # Stop and remove volumes (clean state)
```

## Service Endpoints

All ports are automatically forwarded and accessible:

| Service | Port | URL |
|---------|------|-----|
| Customer API | 7073 | http://localhost:7073/swagger |
| Rating API | 7079 | http://localhost:7079/swagger |
| Policy API | 7077 | http://localhost:7077/swagger |
| Billing API | 7071 | http://localhost:7071/swagger |
| FundsTransferMgt API | 7075 | http://localhost:7075/swagger |
| Cosmos DB Explorer | 8081 | https://localhost:8081/_explorer |
| RabbitMQ | 5672 | (AMQP protocol) |

## Emulator Details

### RabbitMQ

- **Image**: `rabbitmq:3-management`
- **Ports**: 5672 (AMQP), 15672 (management UI)
- **Connection String**: `host=rabbitmq;username=guest;password=guest`

### Cosmos DB Emulator

- **Image**: `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest`
- **Ports**: 8081 (HTTPS), 10251-10254 (partitions)
- **Connection String**: Well-known emulator key (auto-configured)
- **Data Persistence**: Enabled via Docker volume

## Development Workflow

1. **Make code changes** in VS Code
2. **Rebuild specific service**:
   ```bash
   docker-compose build policy-api --no-cache
   docker-compose up -d policy-api
   ```
3. **Test changes**:
   ```bash
   cd test/e2e
   npx playwright test --grep "policy"
   ```
4. **View updated logs**:
   ```bash
   docker logs riskinsure-policy-api-1 --tail 50
   ```

## Troubleshooting

### Emulators Not Ready

If services fail to connect:

```bash
# Check RabbitMQ container
# Check RabbitMQ broker
docker logs rabbitmq

# Check Cosmos DB emulator
curl -k https://localhost:8081/_explorer/index.html
```

### Port Conflicts

If ports are already in use:

```bash
# Find what's using the port
lsof -i :7077

# Or stop all containers and restart
docker-compose down
docker-compose up -d
```

### Clean Slate Reset

```bash
# Stop everything and remove volumes
docker-compose down -v

# Remove dangling images
docker system prune -f

# Restart
docker-compose up -d
```

### Container Build Failures

```bash
# Rebuild from scratch
docker-compose build --no-cache
docker-compose up -d
```

## Resource Limits

GitHub Codespaces free tier:
- **60 hours/month** on 2-core machines
- **Storage**: 15 GB per codespace
- **Max concurrent codespaces**: 2

For team usage, consider upgrading to GitHub Team or Enterprise.

## Cost: $0 💰

With the emulators, you need **zero Azure resources** for development:
- ❌ No managed RabbitMQ broker required for local development
- ❌ No Cosmos DB setup (using free tier saves hassle)
- ✅ Everything runs locally in Codespace
- ✅ Stays within GitHub free tier limits for individual use

## Comparison: Codespace vs Local Development

| Feature | Codespace | Local (Rancher Desktop) |
|---------|-----------|------------------------|
| Setup time | ~5-10 min | ~30-60 min |
| Cross-platform | ✅ Works anywhere | ⚠️ Windows/Mac/Linux specific |
| Team consistency | ✅ Identical for all | ⚠️ Each developer configures |
| Resource usage | ☁️ Cloud resources | 💻 Local resources |
| Cost | Free tier available | Free (your machine) |
| Emulators | ✅ Pre-configured | ⚠️ Manual setup |

## Production vs Development

**Important**: The emulators are for development only!

| Resource | Development | Production |
|----------|-------------|------------|
| Message Transport | RabbitMQ (local) | RabbitMQ/managed broker |
| Cosmos DB | Emulator (local) | Azure Cosmos DB |
| APIs | Docker containers | Azure Container Apps |
| Endpoints | Docker containers | Azure Container Apps with KEDA scaling |

## Next Steps

- See [../docs/getting-started.md](../docs/getting-started.md) for architecture overview
- See [../docs/docker-development.md](../docs/docker-development.md) for Docker tips
- See [../test/e2e/README.md](../test/e2e/README.md) for testing guide
