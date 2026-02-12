# Azure Service Bus Emulator - Docker Setup

Local Azure Service Bus Emulator for RiskInsure development using Docker Compose.

## Prerequisites

- **Docker Desktop** installed and running
- **Note**: SQL Server is included in the docker-compose.yml and will start automatically

## Files

- **`config.json`**: Service Bus namespace configuration (queues, topics, subscriptions)
- **`.env`**: Environment variables (EULA acceptance, SQL password, ports)
- **`docker-compose.yml`**: Docker Compose configuration

## Quick Start

### Start the Emulator

```powershell
docker compose up -d
```

### Check Status

```powershell
docker compose ps
docker compose logs -f servicebus-emulator
```

### List Queues and Topics

```powershell
# Basic list
.\list-entities.ps1

# Detailed view with properties
.\list-entities.ps1 -Method Detailed
```

### Stop the Emulator

```powershell
docker compose down
```

### Restart the Emulator

```powershell
docker compose restart
```

## Configuration

### Environment Variables (.env)

- **`ACCEPT_EULA`**: Must be `Y` to accept the license agreement
- **`SQL_PASSWORD`**: Password for SQL Server (must match your SQL instance)
- **`EMULATOR_HTTP_PORT`**: HTTP API port (default: 5300)
- **`CONFIG_PATH`**: Path to config.json (default: ./config.json)

### Service Bus Configuration (config.json)

The `config.json` file defines:
- **Namespaces**: Service Bus namespaces (currently `sbemulatorns`)
- **Queues**: Message queues for each service endpoint
- **Topics**: Pub/sub topics for domain events
- **Subscriptions**: Topic subscribers (which endpoints listen to which events)

To add new queues/topics, edit `config.json` and restart the emulator.

## Exposed Ports

- **5672**: AMQP protocol (Service Bus connections)
- **5300**: HTTP Management & Health-check API

## Connection Strings

### For NServiceBus Development

Use this connection string in your `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "ServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
  }
}
```

Replace `SAS_KEY_VALUE` with the actual key from the emulator (check emulator logs or management API).

### Health Check

Verify the emulator is running:

```powershell
curl http://localhost:5300/health
```

Expected response: `200 OK`

## Troubleshooting

### Emulator won't start

1. **Check SQL Server**: Ensure SQL Server is running on `localhost:1433`
   ```powershell
   docker ps | Select-String sql
   ```

2. **Check logs**:
   ```powershell
   docker compose logs servicebus-emulator
   ```

3. **Verify config.json**: Ensure the file is valid JSON with no syntax errors

### Connection refused

- Ensure the emulator is running: `docker compose ps`
- Check port 5672 is not blocked by firewall
- Verify the connection string includes `UseDevelopmentEmulator=true`

### Configuration changes not applied

After editing `config.json`, restart the emulator:
```powershell
docker compose restart
```

## Listing Queues and Topics

**Note**: The emulator's Admin API is not fully functional and Azure Service Bus Explorer is not compatible with the emulator.

Use the provided PowerShell script to list all configured entities:

```powershell
# Basic list
.\list-entities.ps1

# Detailed view with properties and subscriptions
.\list-entities.ps1 -Method Detailed

# Export to file
.\list-entities.ps1 | Out-File entities.txt
```

The script reads from `config.json` and displays:
- ✓ All queues with their properties
- ✓ All topics with their subscriptions
- ✓ Configuration details (TTL, lock duration, retry counts)

## Management API

The emulator exposes a health check API on port 5300:

- **Health check**: `GET http://localhost:5300/health`

## Cleanup

To remove the container and network:

```powershell
docker compose down
```

To also remove the image:

```powershell
docker compose down --rmi all
```

## Related Documentation

- [Azure Service Bus Emulator Official Docs](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)
- [RiskInsure Architecture Guide](../../copilot-instructions/constitution.md)
- [NServiceBus Configuration](../../copilot-instructions/messaging-patterns.md)
