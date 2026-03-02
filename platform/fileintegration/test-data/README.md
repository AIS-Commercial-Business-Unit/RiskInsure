# Test Data Directory

This directory contains test files for HTTPS server used in local development and integration testing.

## Directory Structure

```
test-data/
├── transactions/
│   ├── 2025/
│   │   └── 01/
│   │       ├── trans_20250124.csv
│   │       └── trans_20250125.csv
│   └── 2024/
│       └── 12/
│           └── trans_20241231.csv
├── invoices/
│   └── invoice_20250124.pdf
└── health
```

## How to Use

1. Start the HTTPS test server with Docker Compose:
   ```bash
   docker-compose up -d file-retrieval-https
   ```

2. Access test files at:
   - http://localhost:8080/transactions/2025/01/trans_20250124.csv
   - http://localhost:8080/invoices/invoice_20250124.pdf

3. View directory listing:
   - http://localhost:8080/

4. Health check:
   - http://localhost:8080/health

## Adding Test Files

Place files in this directory matching your test scenarios. The directory structure supports date-based paths for testing token replacement logic.
