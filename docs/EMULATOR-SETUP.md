# Quick Start: Emulator Setup

## Option 1: GitHub Codespaces (Recommended) ✨

1. Click **Code** → **Codespaces** → **Create codespace on main**
2. Wait for setup to complete (~5-10 minutes)
3. Run: `docker-compose up -d`
4. Start coding! All emulators are pre-configured.

See [.devcontainer/README.md](.devcontainer/README.md) for full documentation.

## Option 2: Local Development with Emulators

### Prerequisites

- Docker Desktop or Rancher Desktop
- .NET 10 SDK
- Node.js 20+

### Setup Steps

1. **Copy emulator environment variables**:
   ```bash
   cp .env.emulator .env
   ```

2. **Start emulators** (first time):
   ```bash
   docker-compose up -d rabbitmq cosmos-emulator
   ```

3. **Wait for emulators to be ready** (~2-3 minutes):
   ```bash
   # Check Cosmos DB (should return HTML)
   curl -k https://localhost:8081/_explorer/index.html
   
   # Check RabbitMQ (should connect)
   nc -zv localhost 5672
   ```

4. **Start all services**:
   ```bash
   docker-compose up -d
   ```

5. **Verify services are running**:
   ```bash
   docker-compose ps
   ```

6. **Run tests**:
   ```bash
   cd test/e2e
   npm install
   npm test
   ```

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker logs riskinsure-policy-api-1 -f

# Emulators
docker logs rabbitmq -f
docker logs cosmos-emulator -f
```

### Cosmos DB Explorer UI

Open https://localhost:8081/_explorer in your browser to browse:
- Databases
- Containers
- Documents
- Query data

### Stop Services

```bash
# Stop all containers
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

## Option 3: Azure Resources (Production-like)

If you prefer using real Azure resources for local development:

1. **Create resources**:
   ```bash
    # RabbitMQ broker (example: local Docker)
    docker run -d --name rabbitmq \
       -p 5672:5672 -p 15672:15672 \
       rabbitmq:3-management

   # Cosmos DB account (free tier)
   az cosmosdb create \
     --resource-group riskinsure-dev \
     --name riskinsure-dev-cosmos \
     --enable-free-tier true
   ```

2. **Get connection strings**:
   ```bash
   # RabbitMQ
   echo "host=localhost;username=guest;password=guest"

   # Cosmos DB
   az cosmosdb keys list \
     --resource-group riskinsure-dev \
     --name riskinsure-dev-cosmos \
     --type connection-strings \
     --query "connectionStrings[0].connectionString" -o tsv
   ```

3. **Create .env file**:
   ```bash
   cat > .env << EOF
   RABBITMQ_CONNECTION_STRING="host=localhost;username=guest;password=guest"
   COSMOSDB_CONNECTION_STRING="<your-cosmos-connection-string>"
   EOF
   ```

4. **Start services**:
   ```bash
   docker-compose up -d
   ```

## Troubleshooting

### Emulators Not Starting

**Cosmos DB Emulator takes 2-3 minutes to start on first launch.**

Check status:
```bash
docker logs cosmos-emulator --tail 50
```

If it fails, increase Docker memory to at least 4GB:
- Docker Desktop: Settings → Resources → Memory → 4GB
- Rancher Desktop: Preferences → Virtual Machine → Memory → 4GB

### RabbitMQ Connection Errors

Ensure the emulator is running:
```bash
docker logs rabbitmq

# Should see: RabbitMQ startup and health logs
```

If connection fails:
```bash
docker-compose restart rabbitmq
```

### Port Conflicts

If ports 5672 or 8081 are in use:

```bash
# Find process using port
lsof -i :8081   # macOS/Linux
netstat -ano | findstr :8081  # Windows

# Kill the process or change the port in docker-compose.yml
```

### Clean Reset

```bash
# Nuclear option - removes everything
docker-compose down -v
docker system prune -af
docker volume prune -f

# Restart
docker-compose up -d
```

## Performance Tips

### Faster Rebuilds

Only rebuild changed services:
```bash
docker-compose build policy-api --no-cache
docker-compose up -d policy-api
```

### Faster Tests

Run specific tests:
```bash
cd test/e2e
npx playwright test --grep "Class A"
```

### Reduce Resource Usage

Run only services you need:
```bash
# Just Customer and Rating APIs
docker-compose up -d customer-api ratingandunderwriting-api

# Just emulators
docker-compose up -d rabbitmq cosmos-emulator
```

## Comparison: Emulators vs Azure

| Feature | Emulators | Azure Resources |
|---------|-----------|-----------------|
| Cost | Free | ~$10-15/month |
| Setup time | 2-3 min | 10-15 min |
| Internet required | No | Yes |
| Identical to production | 90% | 100% |
| Team sharing | Each developer runs own | Shared namespace |
| Data persistence | Optional | Always |
| Advanced features | Limited | Full |

**Recommendation**: Use emulators for daily development, test against Azure resources before deploying to production.

## Next Steps

- See [docs/getting-started.md](docs/getting-started.md) for architecture overview
- See [docs/docker-development.md](docs/docker-development.md) for Docker workflows
- See [test/e2e/README.md](test/e2e/README.md) for testing guide
- See [copilot-instructions/constitution.md](copilot-instructions/constitution.md) for coding standards
