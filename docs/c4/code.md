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
    SVC["⚙️ CashOrderProcessingService"]:::router

    SVC --> REPO["💾 ICashOrderRepository"]:::db
    SVC --> AUDIT["📋 IAuditTrailService"]:::audit
    SVC --> OPTS["📝 CashOrderProcessingOptions"]:::context
    SVC --> LOG["📊 ILogger"]:::context

    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef db fill:transparent,stroke:#3182CE,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
    classDef context fill:transparent,stroke:#805AD5,stroke-width:3px
```

### ICashOrderRepository
- `GetTotalOrderedTodayAsync` — sum of all confirmed amounts for a client + currency on a given day
- `GetClientDailyLimitAsync` — per-client limit override, or null for default
- `SaveOrdersAsync` — persist accepted orders

### IAuditTrailService
- `RecordAsync` — persist audit entries for **every** processing decision (regulatory requirement)

### CashOrderProcessingOptions
- `DefaultMaxDailyAmount` — fallback daily limit (500,000)
- `SupportedCurrencies` — allowed currency codes (USD, EUR, UAH, etc.)

---

## Domain Model

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    REQ["📥 CashOrderRequest"]:::input
    SVC["⚙️ ProcessBatchAsync"]:::router
    RESULT["📦 BatchProcessingResult"]:::output
    ORDER["✅ CashOrder"]:::gate
    REJORD["❌ RejectedOrder"]:::escalation
    ENTRY["📋 AuditTrailEntry"]:::audit
    LIMIT["📊 ClientDailyLimit"]:::context

    REQ --> SVC
    LIMIT -. "configures" .-> SVC
    SVC --> RESULT
    RESULT --> ORDER
    RESULT --> REJORD
    SVC --> ENTRY

    classDef input fill:transparent,stroke:#319795,stroke-width:3px
    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef gate fill:transparent,stroke:#38A169,stroke-width:3px
    classDef output fill:transparent,stroke:#805AD5,stroke-width:3px
    classDef escalation fill:transparent,stroke:#C53030,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
    classDef context fill:transparent,stroke:#3182CE,stroke-width:3px
```

### CashOrderRequest (input)
| Property | Type | Description |
|----------|------|-------------|
| BankClientId | int | Identifies the bank client |
| Amount | decimal | Requested cash amount |
| Currency | string | ISO currency code |
| RequestedDate | DateTime | When cash is needed |

### CashOrder (accepted output)
| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Unique order identifier |
| Status | OrderStatus | Set to `Validated` on acceptance |
| CreatedAtUtc | DateTime | Timestamp of processing |
| RejectionReason | string? | Null for accepted orders |

### RejectedOrder
| Property | Type | Description |
|----------|------|-------------|
| Request | CashOrderRequest | Original request |
| Reason | string | Human-readable rejection reason |

### AuditTrailEntry
| Property | Type | Description |
|----------|------|-------------|
| EntityType | string | Always `"CashOrder"` |
| Severity | AuditSeverity | `Info` for accepted, `Warning` for rejected |
| BankClientId | int? | For multi-tenant isolation |
| Details | string? | Context about the decision |

### ClientDailyLimit
| Property | Type | Description |
|----------|------|-------------|
| BankClientId | int | Client this limit applies to |
| Currency | string | Currency the limit covers |
| MaxDailyAmount | decimal | Maximum daily amount |

---

## Algorithm: ProcessBatchAsync

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    START["📥 ProcessBatchAsync"]:::input
    GUARD["🛡️ Null guard"]:::gate
    INIT["📝 Init collections"]:::context

    START --> GUARD --> INIT --> LOOP

    LOOP{"🔄 Next request"}:::router

    LOOP --> GET_LIMIT

    GET_LIMIT["📊 Get effective limit"]:::context
    GET_LIMIT --> GET_TOTAL
    GET_TOTAL["📊 Get running total"]:::context
    GET_TOTAL --> CHECK

    CHECK{"Within limit ?"}:::gate
    CHECK -- "no" --> REJ_LIM["❌ Reject: limit exceeded"]:::escalation
    CHECK -- "yes" --> ACCEPT["✅ Accept order"]:::output

    REJ_LIM --> NEXT["➡️ Continue"]:::router
    ACCEPT --> NEXT
    NEXT --> LOOP

    LOOP -- "done" --> SAVE
    SAVE{"Accepted any ?"}:::gate
    SAVE -- "yes" --> PERSIST["💾 SaveOrdersAsync"]:::db
    SAVE -- "no" --> AUDIT
    PERSIST --> AUDIT
    AUDIT["📋 RecordAsync"]:::audit
    AUDIT --> RET["📦 Return result"]:::output

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

### Step-by-step

1. **Guard** — throw `ArgumentNullException` if requests is null
2. **Initialize** — empty lists for accepted, rejected, audit entries; dictionary for running totals
3. **For each request:**
   - Get effective limit: `GetClientDailyLimitAsync()` → fallback to `DefaultMaxDailyAmount`
   - Get running total: `GetTotalOrderedTodayAsync()` + batch running total for same (clientId, currency)
   - If `currentTotal + amount > limit` → reject with `Warning` audit
   - Otherwise → create `CashOrder` with `Status = Validated`, `CreatedAtUtc = UtcNow`, update running total, add `Info` audit
4. **Persist** — `SaveOrdersAsync` only if at least one order accepted
5. **Audit** — `RecordAsync` always called (even for empty batches)
6. **Return** — `BatchProcessingResult` with accepted and rejected lists

---

## Audit Entry Rules

| Scenario | Action | Severity |
|----------|--------|----------|
| Order accepted | `OrderAccepted` | Info |
| Limit exceeded | `OrderRejected` | Warning |
| Empty batch | `EmptyBatchProcessed` | Info |

---

## Interface Contracts

```csharp
// Core service — YOUR IMPLEMENTATION
Task<BatchProcessingResult> ProcessBatchAsync(
    IEnumerable<CashOrderRequest> requests,
    CancellationToken cancellationToken = default);
```

```csharp
// Repository — mocked in tests
Task<decimal> GetTotalOrderedTodayAsync(int bankClientId, string currency, DateTime date, CancellationToken ct);
Task SaveOrdersAsync(IEnumerable<CashOrder> orders, CancellationToken ct);
Task<ClientDailyLimit?> GetClientDailyLimitAsync(int bankClientId, string currency, CancellationToken ct);
```

```csharp
// Audit trail — mocked in tests (⭐ star challenge)
Task RecordAsync(IEnumerable<AuditTrailEntry> entries, CancellationToken ct);
```
