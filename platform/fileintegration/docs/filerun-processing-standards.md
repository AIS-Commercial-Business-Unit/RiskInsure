# FileRun Processing Standards

**Domain**: ACH/NACHA File Processing | **Version**: 1.0.0 | **Last Updated**: 2026-02-02

This document defines domain-specific standards for the FileRun processing system (ACH/NACHA file ingestion and payment instruction processing).

---

## Domain Language

### Required Terminology

- **PaymentInstruction** or **AchPaymentInstruction**: Individual ACH transaction from NACHA file
- **FileRun**: Single execution instance of file processing from ingestion through completion
- **BatchId**: NACHA batch number within the file
- **TraceNumber**: NACHA trace/sequence number uniquely identifying the instruction within the file

### Prohibited Terms

- ❌ **"Entry"**: NACHA technical term - always use **PaymentInstruction**
- ❌ **"Transaction"**: Ambiguous - use **PaymentInstruction**
- ❌ **"Record"**: Database term - use domain entity name

---

## Data Model

### Cosmos DB Partition Strategy

**Container**: Single container for all FileRun processing documents  
**Partition Key**: `/fileRunId`

**Document Types**:
- `FileRun`: Aggregate document tracking overall file processing state
- `PaymentInstruction`: Individual ACH payment instruction documents

**FileRun Document Structure**:
```json
{
  "id": "guid",
  "fileRunId": "guid",
  "type": "FileRun",
  "fileName": "string",
  "fileSize": 0,
  "storageUri": "string",
  "status": "Pending|Processing|Completed|CompletedWithErrors|Failed",
  "totalInstructions": 0,
  "succeededCount": 0,
  "failedCount": 0,
  "pendingCount": 0,
  "createdUtc": "datetimeoffset",
  "completedUtc": "datetimeoffset"
}
```

**PaymentInstruction Document Structure**:
```json
{
  "id": "string (composite: fileRunId-traceNumber)",
  "fileRunId": "guid",
  "type": "PaymentInstruction",
  "paymentInstructionId": "string",
  "batchId": "string",
  "traceNumber": "string",
  "status": "Pending|Processed|Failed",
  "payloadRef": "string",
  "failureReason": "string (if failed)",
  "errorCode": "string (if failed)",
  "processedUtc": "datetimeoffset"
}
```

---

## State Transition Rules

### FileRun State Machine

```
Pending → Processing → [Completed | CompletedWithErrors]
   ↓
Failed (on critical errors)
```

### PaymentInstruction State Machine

```
Pending → [Processed | Failed]
Failed → Processed (on successful replay)
```

### Critical Count Updates

When PaymentInstruction transitions state, FileRun counts MUST be updated atomically:

**Pending → Processed**:
- Decrement `pendingCount`
- Increment `succeededCount`

**Pending → Failed**:
- Decrement `pendingCount`
- Increment `failedCount`

**Failed → Processed (Replay)**:
- Decrement `failedCount`
- Increment `succeededCount`
- Update FileRun `status` to `Completed` if all instructions now succeeded

---

## Message Contracts

### FileReceived

Published when a new ACH/NACHA file has been ingested.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `FileName` (string)
- `FileSize` (long)
- `StorageUri` (string)
- `IdempotencyKey` (string)

### AchPaymentInstructionReady

Published when an individual payment instruction has been parsed and is ready for processing.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `PaymentInstructionId` (string)
- `BatchId` (string)
- `TraceNumber` (string)
- `IdempotencyKey` (string)
- `PayloadRef` (string)

### AchPaymentInstructionProcessed

Published when a payment instruction has been successfully processed.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `PaymentInstructionId` (string)
- `BatchId` (string)
- `TraceNumber` (string)
- `IdempotencyKey` (string)
- `PayloadRef` (string)

### AchPaymentInstructionFailed

Published when a payment instruction has failed processing.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `PaymentInstructionId` (string)
- `BatchId` (string)
- `TraceNumber` (string)
- `IdempotencyKey` (string)
- `PayloadRef` (string)
- `FailureReason` (string)
- `ErrorCode` (string)

### FileCompleted

Published when all payment instructions have been processed successfully.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `TotalInstructions` (int)
- `IdempotencyKey` (string)

### FileCompletedWithErrors

Published when all payment instructions have been processed but some failed.

**Fields**:
- `MessageId` (Guid)
- `OccurredUtc` (DateTimeOffset)
- `FileRunId` (Guid)
- `TotalInstructions` (int)
- `SucceededCount` (int)
- `FailedCount` (int)
- `IdempotencyKey` (string)

---

## Component Architecture

### Logic Apps Standard (Ingestion)

**Responsibilities**:
- Receive ACH/NACHA files from external sources
- Store raw files in Azure Blob Storage
- Publish `FileReceived` event to Service Bus
- Handle operational notifications and error handling
- Trigger downstream workflows on completion events

### Parser Endpoint (NServiceBus)

**Responsibilities**:
- Consume `FileReceived` events
- Parse NACHA file format
- Create FileRun document with initial counts
- Publish `AchPaymentInstructionReady` event per instruction

**Hosting**: Azure Container Apps with KEDA Service Bus scaling

### Processor Endpoint (NServiceBus)

**Responsibilities**:
- Consume `AchPaymentInstructionReady` events
- Validate business rules for payment instructions
- Create PaymentInstruction documents
- Publish `AchPaymentInstructionProcessed` or `AchPaymentInstructionFailed`

**Hosting**: Azure Container Apps with KEDA Service Bus scaling

### Control Plane Endpoint (NServiceBus)

**Responsibilities**:
- Consume `AchPaymentInstructionProcessed` and `AchPaymentInstructionFailed` events
- Update FileRun aggregate counts atomically
- Detect file completion (all instructions processed)
- Publish `FileCompleted` or `FileCompletedWithErrors`

**Hosting**: Azure Container Apps with KEDA Service Bus scaling

---

## Observability Standards

### Required Log Fields

Every log entry for FileRun processing MUST include:

**File-Level Operations**:
- `fileRunId` (Guid)
- `fileName` (string)
- `operationName` (string)

**Instruction-Level Operations**:
- `fileRunId` (Guid)
- `paymentInstructionId` (string)
- `batchId` (string)
- `traceNumber` (string)
- `operationName` (string)

**All Operations**:
- `correlationId` (from NServiceBus message context)

### Sample Log Statements

```csharp
_logger.LogInformation(
    "FileRun {FileRunId} processing started for file {FileName}",
    fileRunId, fileName);

_logger.LogInformation(
    "PaymentInstruction {PaymentInstructionId} in FileRun {FileRunId} processed successfully",
    paymentInstructionId, fileRunId);

_logger.LogWarning(
    "PaymentInstruction {PaymentInstructionId} in FileRun {FileRunId} failed: {FailureReason}",
    paymentInstructionId, fileRunId, failureReason);
```

---

## Testing Standards

### Repository Integration Tests

Test FileRun count updates during state transitions:

```csharp
[Fact]
public async Task UpdateFileRunCounts_FailedToSucceeded_UpdatesCountsCorrectly()
{
    // Arrange
    var fileRun = new FileRun 
    { 
        FileRunId = Guid.NewGuid(),
        TotalInstructions = 10,
        SucceededCount = 8,
        FailedCount = 2,
        PendingCount = 0
    };
    await _repository.CreateAsync(fileRun);
    
    // Act
    await _repository.UpdateCountsAsync(fileRun.FileRunId, 
        succeededDelta: +1, failedDelta: -1);
    
    // Assert
    var updated = await _repository.GetByIdAsync(fileRun.FileRunId);
    updated.SucceededCount.Should().Be(9);
    updated.FailedCount.Should().Be(1);
}
```

### Handler Idempotency Tests

```csharp
[Fact]
public async Task Handle_DuplicateMessage_DoesNotCreateDuplicate()
{
    // Arrange
    var message = new AchPaymentInstructionReady(/* ... */);
    
    // Act - handle twice
    await _handler.Handle(message, _context);
    await _handler.Handle(message, _context);
    
    // Assert - only one document created
    var instructions = await _repository.GetByFileRunIdAsync(message.FileRunId);
    instructions.Should().ContainSingle();
}
```

---

## Related Documents

- [architecture.md](architecture.md) - System architecture overview
- [message-contracts.md](message-contracts.md) - Complete message contract specifications
- [../../.specify/memory/constitution.md](../../.specify/memory/constitution.md) - Architecture constitution
