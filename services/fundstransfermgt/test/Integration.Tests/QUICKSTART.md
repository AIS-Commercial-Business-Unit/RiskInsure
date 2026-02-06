# Quick Start Guide - Fund Transfer E2E Tests

This guide will help you run the comprehensive fund transfer end-to-end integration tests.

## Prerequisites Checklist

- [ ] .NET 10 SDK installed
- [ ] Node.js 18+ installed
- [ ] Cosmos DB Emulator running
- [ ] Azure Service Bus (connection string in appsettings.Development.json)

## Step-by-Step Setup

### 1. Start Cosmos DB Emulator

**Option A: Windows (Installed Emulator)**
```powershell
# Start from Start Menu or
Start-Process "C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"
```

**Option B: Docker**
```powershell
docker run -p 8081:8081 -p 10251-10254:10251-10254 `
  --name cosmos-emulator `
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 `
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

Wait for emulator to be ready (check https://localhost:8081/_explorer/index.html)

### 2. Configure API Settings

Ensure you have `appsettings.Development.json` in the API project:

```powershell
cd c:\Dev\AIS-Commercial-Business-Unit\RiskInsure\services\fundstransfermgt\src\Api
```

Check the file exists (should NOT be committed to Git):
```powershell
Get-Item appsettings.Development.json
```

If missing, copy from template:
```powershell
Copy-Item appsettings.Development.json.template appsettings.Development.json
```

Edit and add your connection strings:
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5...",
    "ServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/..."
  }
}
```

### 3. Start the API

**Terminal 1 - Fund Transfer API:**
```powershell
cd c:\Dev\AIS-Commercial-Business-Unit\RiskInsure\services\fundstransfermgt\src\Api
dotnet run
```

Wait for the message:
```
Now listening on: http://localhost:7073
```

### 4. Install Test Dependencies (First Time Only)

**Terminal 2 - Tests:**
```powershell
cd c:\Dev\AIS-Commercial-Business-Unit\RiskInsure\services\fundstransfermgt\test\Integration.Tests

# Install Node.js packages
npm install

# Install Playwright browsers (first time only)
npx playwright install chromium
```

### 5. Run the Tests

**Option A: UI Mode (Recommended for Development)**
```powershell
npm run test:ui
```

This opens an interactive UI where you can:
- Select which tests to run
- Watch tests execute in real-time
- See request/response details
- Debug failures easily

**Option B: Run All E2E Tests**
```powershell
npm run test:e2e
```

**Option C: Run Specific Test**
```powershell
npx playwright test --grep "Complete credit card fund transfer workflow"
```

**Option D: Debug Mode**
```powershell
npm run test:debug
```

## What the Tests Do

### Test 1: Complete Credit Card Fund Transfer
1. Creates a Visa credit card payment method
2. Verifies the payment method can be retrieved
3. Initiates a $150 fund transfer
4. Retrieves the transfer by ID
5. Gets customer transfer history
6. Creates a second transfer ($75.50)
7. Verifies both transfers in history

**Duration**: ~8-12 seconds

### Test 2: Complete ACH Fund Transfer
1. Creates a checking account payment method
2. Initiates a $500 fund transfer
3. Validates transfer completion

**Duration**: ~5-8 seconds

### Test 3: Multi-Payment Method Scenario
1. Creates credit card
2. Creates ACH account
3. Transfers $250 via credit card
4. Transfers $1,000 via ACH
5. Verifies both in history

**Duration**: ~10-15 seconds

### Test 4-8: Error Handling
Tests various error scenarios:
- Invalid payment method ID → 400 Bad Request
- Inactive payment method → 400 Bad Request
- Wrong customer's payment method → 400 Bad Request
- Large amount transfers ($10,000) → Success
- Non-existent transfer retrieval → 404 Not Found

**Duration**: ~5-8 seconds each

## Expected Output

### Successful Test Run
```
🚀 Starting E2E test with customerId: CUST-1738742400000

📋 STEP 1: Adding credit card payment method...
✅ Credit card added successfully
   Payment Method ID: 123e4567-e89b-12d3-a456-426614174000
   Card: Visa ending in 0366
   Status: Validated

📋 STEP 2: Verifying payment method retrieval...
✅ Payment method retrieved successfully

📋 STEP 3: Initiating fund transfer...
✅ Fund transfer initiated and settled successfully
   Transaction ID: 987fcdeb-51a0-4b2d-8e3a-123456789abc
   Amount: $150
   Status: Settled
   Purpose: E2E Test - Premium Payment

📋 STEP 4: Retrieving transfer by ID...
✅ Transfer retrieved successfully by ID

📋 STEP 5: Retrieving customer transfer history...
✅ Customer transfer history retrieved
   Total transfers: 1

📋 STEP 6: Creating second transfer to verify history...
✅ Second transfer created successfully
   Transaction count for customer: 2

🎉 E2E test completed successfully!
```

## Troubleshooting

### API Won't Start
**Error**: Port 7073 already in use

**Solution**:
```powershell
# Find process using port
netstat -ano | findstr :7073

# Kill process (use PID from above)
Stop-Process -Id <PID> -Force
```

### Cosmos DB Connection Failed
**Error**: Unable to connect to Cosmos DB

**Solutions**:
1. Check emulator is running (visit https://localhost:8081/_explorer/index.html)
2. Verify connection string in appsettings.Development.json
3. Restart emulator
4. Check firewall isn't blocking localhost:8081

### Service Bus Connection Failed
**Error**: Cannot connect to Service Bus

**Solutions**:
1. Verify Service Bus connection string in appsettings.Development.json
2. Check Service Bus namespace exists in Azure
3. Verify connection string has correct permissions

### Tests Fail with "Payment method not found"
**Cause**: Database state issues or previous test data

**Solution**:
1. Restart Cosmos DB Emulator (clears all data)
2. Restart API
3. Re-run tests

### Tests Are Slow
**Cause**: Cosmos DB Emulator on first run initializes partitions

**Solution**:
- First test run is slower (~30 seconds)
- Subsequent runs are faster (~5-10 seconds per test)
- Keep emulator running between test sessions

## Next Steps

After successful test run:

1. **View HTML Report**
   ```powershell
   npm run test:report
   ```

2. **Run Individual Test Suites**
   ```powershell
   npm run test:payment-methods  # Payment method lifecycle
   npm run test:e2e              # Fund transfer E2E
   ```

3. **Check Test Coverage**
   Review `TEST-COVERAGE.md` for complete test matrix

4. **Review API Logs**
   Check Terminal 1 (API) for structured logs with correlation IDs

## Test Data

Tests automatically generate unique data:
- **Customer IDs**: `CUST-{timestamp}`
- **Payment Method IDs**: Random UUIDs
- **Card Numbers**: Valid test cards (Visa 4532015112830366)
- **Routing Numbers**: Valid test routing (011000015)

All test data is isolated per test run to prevent conflicts.

## Resources

- **Test Files**: `tests/fund-transfer-e2e.spec.ts`
- **Configuration**: `playwright.config.ts`
- **Coverage Report**: `TEST-COVERAGE.md`
- **API Docs**: `../../docs/`

## Support

If you encounter issues:
1. Check API logs in Terminal 1
2. Check test console output for detailed steps
3. Run in debug mode: `npm run test:debug`
4. Review test file comments for expected behavior
