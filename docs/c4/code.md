# C4 — Level 4: Code

<div align="center">

*Internal structure of the Order Processing Service at the code level*

</div>

---

## Service Dependencies

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    subgraph service ["CashOrderProcessingService"]
        direction TB
        SVC["⚙️ ProcessBatchAsync\nCore orchestration method"]:::router
    end

    SVC -- "Validates orders,\nchecks limits,\nsaves accepted" --> REPO["💾 ICashOrderRepository\nGetTotalOrderedTodayAsync\nGetClientDailyLimitAsync\nSaveOrdersAsync"]:::db

    SVC -- "Records EVERY\nprocessing decision" --> AUDIT["📋 IAuditTrailService\nRecordAsync"]:::audit

    SVC -- "Reads supported currencies\nand default daily limit" --> OPTS["📝 CashOrderProcessingOptions\nDefaultMaxDailyAmount\nSupportedCurrencies"]:::context

    SVC -- "Structured logging" --> LOG["📊 ILogger"]:::context

    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef db fill:transparent,stroke:#3182CE,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
    classDef context fill:transparent,stroke:#805AD5,stroke-width:3px
```

---

## Domain Model Relationships

```mermaid
---
config:
  look: handDrawn
---
flowchart LR
    subgraph input ["Input"]
        REQ["📥 CashOrderRequest\nBankClientId · Amount\nCurrency · RequestedDate"]:::input
    end

    subgraph processing ["Processing"]
        direction TB
        SVC["⚙️ ProcessBatchAsync"]:::router
        LIMIT["📊 ClientDailyLimit\nBankClientId · Currency\nMaxDailyAmount"]:::gate
    end

    subgraph output ["Output"]
        direction TB
        RESULT["📦 BatchProcessingResult\nAcceptedOrders · RejectedOrders\nTotalCount · AcceptedCount"]:::output

        subgraph accepted ["Accepted"]
            ORDER["✅ CashOrder\nId · BankClientId · Amount\nCurrency · RequestedDate\nStatus = Validated\nCreatedAtUtc"]:::gate
        end

        subgraph rejected ["Rejected"]
            REJORD["❌ RejectedOrder\nRequest · Reason"]:::escalation
        end
    end

    subgraph audit ["Audit"]
        ENTRY["📋 AuditTrailEntry\nId · EntityType · EntityId\nAction · Severity\nDetails · TimestampUtc\nBankClientId"]:::audit
    end

    REQ -- "Processed by" --> SVC
    LIMIT -. "Configures limits for" .-> SVC
    SVC -- "Produces" --> RESULT
    RESULT --> ORDER
    RESULT --> REJORD
    REJORD -. "Wraps original" .-> REQ
    SVC -- "Emits for each\norder processed" --> ENTRY

    classDef input fill:transparent,stroke:#319795,stroke-width:3px
    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef gate fill:transparent,stroke:#38A169,stroke-width:3px
    classDef output fill:transparent,stroke:#805AD5,stroke-width:3px
    classDef escalation fill:transparent,stroke:#C53030,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
```

---

## Algorithm: ProcessBatchAsync

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    START["📥 ProcessBatchAsync\n(requests, cancellationToken)"]:::input
    GUARD["🛡️ Guard: throw\nArgumentNullException\nif requests is null"]:::gate
    INIT["📝 Initialize:\nacceptedOrders, rejectedOrders,\nauditEntries, runningTotals"]:::context

    START --> GUARD --> INIT --> LOOP

    LOOP{"🔄 For each\nrequest in batch"}:::router

    VAMT["✅ Validate:\namount > 0 ?"]:::agent
    LOOP --> VAMT

    VAMT -- "❌ Invalid" --> REJ_AMT["Reject:\nInvalid amount\nSeverity: Warning"]:::escalation
    VAMT -- "✅ Valid" --> VCUR

    VCUR["✅ Validate:\ncurrency in\nSupportedCurrencies ?"]:::agent
    VCUR -- "❌ Invalid" --> REJ_CUR["Reject:\nUnsupported currency\nSeverity: Warning"]:::escalation
    VCUR -- "✅ Valid" --> GET_LIMIT

    GET_LIMIT["📊 Get effective limit:\nclientLimit = GetClientDailyLimitAsync\neffective = clientLimit ?? default"]:::context

    GET_LIMIT --> GET_TOTAL

    GET_TOTAL["📊 Get current total:\ndbTotal = GetTotalOrderedTodayAsync\nbatchTotal = runningTotals\ncurrent = dbTotal + batchTotal"]:::context

    GET_TOTAL --> CHECK_LIMIT

    CHECK_LIMIT{"current + amount\n≤ effective limit ?"}:::gate
    CHECK_LIMIT -- "❌ Exceeded" --> REJ_LIMIT["Reject:\nDaily limit exceeded\nSeverity: Warning"]:::escalation
    CHECK_LIMIT -- "✅ Within limit" --> ACCEPT

    ACCEPT["✅ Accept:\nCreate CashOrder (new Guid)\nStatus = Validated\nCreatedAtUtc = UtcNow\nUpdate runningTotals\nSeverity: Info"]:::output

    REJ_AMT --> NEXT["➡️ Next\nrequest"]:::router
    REJ_CUR --> NEXT
    REJ_LIMIT --> NEXT
    ACCEPT --> NEXT
    NEXT --> LOOP

    LOOP -- "✅ All done" --> SAVE

    SAVE{"Any accepted\norders?"}:::gate
    SAVE -- "Yes" --> PERSIST["💾 SaveOrdersAsync\n(acceptedOrders)"]:::db
    SAVE -- "No" --> AUDIT_STEP
    PERSIST --> AUDIT_STEP

    AUDIT_STEP["📋 RecordAsync\n(all audit entries)"]:::audit
    AUDIT_STEP --> RETURN["📦 Return\nBatchProcessingResult"]:::output

    classDef input fill:transparent,stroke:#319795,stroke-width:3px
    classDef gate fill:transparent,stroke:#38A169,stroke-width:3px
    classDef context fill:transparent,stroke:#3182CE,stroke-width:3px
    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef agent fill:transparent,stroke:#E53E50,stroke-width:3px
    classDef escalation fill:transparent,stroke:#C53030,stroke-width:3px
    classDef output fill:transparent,stroke:#805AD5,stroke-width:3px
    classDef db fill:transparent,stroke:#3182CE,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
```

---

## Audit Entry Construction Rules

| Scenario | EntityType | Action | Severity | Details |
|----------|-----------|--------|----------|---------|
| Order accepted | `CashOrder` | `OrderAccepted` | Info | Amount, currency, client ID |
| Rejected: invalid amount | `CashOrder` | `OrderRejected` | Warning | `Invalid amount: {amount}` |
| Rejected: unsupported currency | `CashOrder` | `OrderRejected` | Warning | `Unsupported currency: {currency}` |
| Rejected: limit exceeded | `CashOrder` | `OrderRejected` | Warning | `Daily limit exceeded: {current}/{limit} {currency}` |
| Empty batch processed | `CashOrder` | `EmptyBatchProcessed` | Info | `No orders in batch` |

---

## Interface Contracts

### ICashOrderProcessingService
```csharp
Task<BatchProcessingResult> ProcessBatchAsync(
    IEnumerable<CashOrderRequest> requests,
    CancellationToken cancellationToken = default);
```

### ICashOrderRepository
```csharp
Task<decimal> GetTotalOrderedTodayAsync(int bankClientId, string currency, DateTime date, CancellationToken ct);
Task SaveOrdersAsync(IEnumerable<CashOrder> orders, CancellationToken ct);
Task<ClientDailyLimit?> GetClientDailyLimitAsync(int bankClientId, string currency, CancellationToken ct);
```

### IAuditTrailService
```csharp
Task RecordAsync(IEnumerable<AuditTrailEntry> entries, CancellationToken ct);
```
