# Running Modernization Patterns Chatbot Locally

This guide shows how to run the Modernization Patterns RAG chatbot on your local machine for development and testing.

---

## Prerequisites

- **.NET 10 SDK** installed
- **Node.js 18+** and npm installed
- **Azure credentials** (for accessing production Azure OpenAI, Azure Search, and Cosmos DB)
- **Visual Studio Code** or any code editor

---

## Step 1: Configure Local Settings

1. Navigate to the Chat API directory:
   ```powershell
   cd C:\RiskInsure\RiskInsure\platform\modernizationpatterns\Api\chat
   ```

2. Copy the template file to create your local settings:
   ```powershell
   cp appsettings.Development.json.template appsettings.Development.json
   ```

3. Edit `appsettings.Development.json` with your Azure credentials:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft": "Warning"
       }
     },
     "ConnectionStrings": {
       "CosmosDb": "AccountEndpoint=https://riskinsure-dev-cosmos.documents.azure.com:443/;AccountKey=<YOUR_KEY>"
     },
     "CosmosDb": {
       "DatabaseName": "modernization-patterns-db"
     },
     "AzureOpenAI": {
       "Endpoint": "https://riskinsure-foundry.openai.azure.com/",
       "ApiKey": "<YOUR_OPENAI_KEY>",
       "ChatDeploymentName": "gpt-4.1",
       "EmbeddingDeploymentName": "text-embedding-3-small"
     },
     "AzureSearch": {
       "Endpoint": "https://riskinsure-dev-aisearch.search.windows.net",
       "ApiKey": "<YOUR_SEARCH_KEY>",
       "IndexName": "modernization-patterns"
     },
     "ApplicationInsights": {
       "InstrumentationKey": "<YOUR_APP_INSIGHTS_KEY>"
     }
   }
   ```

   > **Note:** This file is in `.gitignore` and will not be committed to the repository.

---

## Step 2: Start the Backend API

1. Open a PowerShell terminal

2. Navigate to the Chat API directory:
   ```powershell
   cd C:\RiskInsure\RiskInsure\platform\modernizationpatterns\Api\chat
   ```

3. Set the environment variable:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   ```

4. Build and run the API:
   ```powershell
   dotnet build
   dotnet run
   ```

5. Verify the API is running:
   ```powershell
   Invoke-WebRequest http://localhost:5000/health
   ```

   Expected response: `200 OK`

---

## Step 3: Start the Frontend

1. Open a **new** PowerShell terminal (keep the API terminal running)

2. Navigate to the frontend directory:
   ```powershell
   cd C:\RiskInsure\RiskInsure\platform\modernizationpatterns
   ```

3. Install dependencies (first time only):
   ```powershell
   npm install
   ```

4. Start the Vite dev server:
   ```powershell
   npm run dev
   ```

5. The frontend will start at:
   ```
   http://localhost:5173
   ```

---

## Step 4: Test the Chatbot

### Option 1: Browser UI

1. Open your browser to: **http://localhost:5173**

2. Look for the **blue chat widget** in the **bottom-right corner**

3. Click to open the chat interface

4. Type a question like:
   - "what is saga pattern?"
   - "tell me about CQRS"
   - "what is event sourcing?"

5. You should see:
   - ✅ Embedding complete
   - ✅ Search complete (found X patterns)
   - ✅ Streaming AI response with relevant information

### Option 2: API Test (PowerShell)

```powershell
# Create a new conversation
$conv = Invoke-WebRequest "http://localhost:5000/api/chat/new?userId=test" -Method POST
$conversationId = ($conv.Content | ConvertFrom-Json).conversationId

# Send a chat message
$body = @{
    userId = "test"
    conversationId = $conversationId
    message = "what is saga pattern?"
} | ConvertTo-Json

$response = Invoke-WebRequest `
    "http://localhost:5000/api/chat/stream" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -TimeoutSec 60

# View the response
$response.Content -split "`n" | Select-Object -First 30
```

Expected output:
```
event: embedding_complete
data: Query embedded successfully

event: search_complete
data: Found 5 relevant patterns

event: token
data: Saga / process mana

event: token
data: ger is a pattern fo
...
```

---

## Troubleshooting

### API won't start

**Error:** `The process cannot access the file because it is being used by another process`

**Solution:**
```powershell
# Kill any running instances
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

# Wait a moment
Start-Sleep -Seconds 2

# Try again
dotnet build
dotnet run
```

### Frontend won't connect to API

**Error:** CORS or connection refused errors

**Solution:**
1. Verify API is running: `Invoke-WebRequest http://localhost:5000/health`
2. Check that frontend is pointing to correct API URL (should be `http://localhost:5000`)
3. Ensure `ASPNETCORE_ENVIRONMENT` is set to `Development` (enables CORS)

### Empty or error responses

**Error:** 411 Length Required, 400 Bad Request, or empty responses

**Solution:**
1. Verify `appsettings.Development.json` has correct Azure credentials
2. Check that Azure Search index `modernization-patterns` exists and has documents
3. Ensure Azure OpenAI deployments exist:
   - `gpt-4.1` (chat)
   - `text-embedding-3-small` (embeddings)

### Search returns no results

**Problem:** Search completes but finds 0 patterns

**Solution:**
1. Verify the Azure Search index has documents:
   - Go to Azure Portal → Azure AI Search → Indexes → modernization-patterns
   - Check document count (should be 41 patterns)
2. If empty, run the reindex workflow:
   - Go to GitHub Actions → "Modernization Patterns - Reindex on Content Changes"
   - Click "Run workflow" → Select "Full Clean Reindex"

---

## Port Configuration

| Service | Port | URL |
|---------|------|-----|
| Chat API | 5000 | http://localhost:5000 |
| Frontend (Vite) | 5173 | http://localhost:5173 |

---

## Quick Start Script

Save this as `start-local.ps1`:

```powershell
# Start Chat API in background
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd C:\RiskInsure\RiskInsure\platform\modernizationpatterns\Api\chat; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run"
) -WindowStyle Normal

Write-Host "Starting API..." -ForegroundColor Cyan
Start-Sleep -Seconds 10

# Start Frontend in background
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd C:\RiskInsure\RiskInsure\platform\modernizationpatterns; npm run dev"
) -WindowStyle Normal

Write-Host "Starting Frontend..." -ForegroundColor Cyan
Start-Sleep -Seconds 8

# Open browser
Start-Process "http://localhost:5173"

Write-Host "Done! Chatbot running at http://localhost:5173" -ForegroundColor Green
```

Run with:
```powershell
.\start-local.ps1
```

---

## Comparing Local vs Azure

| Aspect | Local | Azure Deployed |
|--------|-------|----------------|
| **API** | http://localhost:5000 | https://modernizationpatterns-chat-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io |
| **Frontend** | http://localhost:5173 | Azure Static Web App URL |
| **Config** | appsettings.Development.json | Azure Key Vault secrets |
| **Data** | Production Azure services | Same Azure services |
| **Environment** | Development mode (verbose logs, CORS enabled) | Production mode |

Both environments use the **same Azure resources** (Cosmos DB, OpenAI, Search), so ensure your local changes don't affect production data if using a shared environment.

---

## Next Steps

After verifying the chatbot works locally:

1. **Commit and push your changes** to the Dev branch
2. **Trigger the CI/CD pipeline** to deploy to Azure
3. **Update Key Vault secrets** if needed (via GitHub Actions workflow)
4. **Test the Azure deployment** using the Static Web App URL

---

## Related Documentation

- [Chat API Documentation](Api/chat/API.md)
- [Frontend Architecture](src/components/ChatWidget/COMPONENTS.md)
- [Deployment Guide](../../docs/deployment.md)
- [Azure Configuration](../../platform/infra/services/modernizationpatterns-app.tf)
