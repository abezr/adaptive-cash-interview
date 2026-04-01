# C4 — Level 4: Code

<div align="center">

*Internal structure of the Order Processing Service at the code level*

</div>

---

## Class Diagram

```mermaid
---
config:
  look: handDrawn
---
classDiagram
    class ICashOrderProcessingService {
        <<interface>>
        +ProcessBatchAsync(requests, ct) Task~BatchProcessingResult~
    }

    class CashOrderProcessingService {
        -ICashOrderRepository _repository
        -IAuditTrailService _auditTrailService
        -CashOrderProcessingOptions _options
        -ILogger _logger
        +ProcessBatchAsync(requests, ct) Task~BatchProcessingResult~
    }

    class ICashOrderRepository {
        <<interface>>
        +GetTotalOrderedTodayAsync(clientId, currency, date, ct) Task~decimal~
        +SaveOrdersAsync(orders, ct) Task
        +GetClientDailyLimitAsync(clientId, currency, ct) Task~ClientDailyLimit~
    }

    class IAuditTrailService {
        <<interface>>
        +RecordAsync(entries, ct) Task
    }

    class CashOrderProcessingOptions {
        +decimal DefaultMaxDailyAmount
        +HashSet~string~ SupportedCurrencies
    }

    class CashOrderRequest {
        +int BankClientId
        +decimal Amount
        +string Currency
        +DateTime RequestedDate
    }

    class CashOrder {
        +Guid Id
        +int BankClientId
        +decimal Amount
        +string Currency
        +DateTime RequestedDate
        +OrderStatus Status
        +DateTime CreatedAtUtc
        +string RejectionReason
    }

    class BatchProcessingResult {
        +List~CashOrder~ AcceptedOrders
        +List~RejectedOrder~ RejectedOrders
        +int TotalCount
    }

    class RejectedOrder {
        +CashOrderRequest Request
        +string Reason
    }

    class AuditTrailEntry {
        +Guid Id
        +string EntityType
        +string EntityId
        +string Action
        +AuditSeverity Severity
        +string Details
        +DateTime TimestampUtc
        +int BankClientId
    }

    CashOrderProcessingService ..|> ICashOrderProcessingService
    CashOrderProcessingService --> ICashOrderRepository : uses
    CashOrderProcessingService --> IAuditTrailService : uses
    CashOrderProcessingService --> CashOrderProcessingOptions : reads config
    CashOrderProcessingService ..> CashOrderRequest : processes
    CashOrderProcessingService ..> CashOrder : creates
    CashOrderProcessingService ..> BatchProcessingResult : returns
    CashOrderProcessingService ..> AuditTrailEntry : creates
    BatchProcessingResult --> CashOrder : contains
    BatchProcessingResult --> RejectedOrder : contains
    RejectedOrder --> CashOrderRequest : wraps
```

---

## Method Flow: ProcessBatchAsync

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    START["ProcessBatchAsync(requests)"]:::input
    GUARD["Guard: null check"]:::gate
    INIT["Initialize:\naccepted, rejected,\naudit entries,\nrunning totals"]:::context

    START --> GUARD --> INIT

    LOOP{"For each\nrequest"}:::router
    INIT --> LOOP

    VAMT["Validate\namount > 0"]:::agent
    LOOP --> VAMT

    VAMT -- "Invalid" --> REJ1["Add to rejected\n+ Warning audit"]:::escalation
    VAMT -- "Valid" --> VCUR["Validate\ncurrency supported"]:::agent

    VCUR -- "Invalid" --> REJ2["Add to rejected\n+ Warning audit"]:::escalation
    VCUR -- "Valid" --> GLIM["Get daily limit\n(client-specific or default)"]:::context

    GLIM --> GTOT["Get current total\n(DB + batch running total)"]:::context
    GTOT --> CHKLIM{"Within\nlimit?"}:::gate

    CHKLIM -- "Exceeded" --> REJ3["Add to rejected\n+ Warning audit"]:::escalation
    CHKLIM -- "OK" --> ACC["Create CashOrder\nStatus = Validated\n+ Info audit\n+ update running total"]:::output

    REJ1 --> NEXT["Next request"]:::router
    REJ2 --> NEXT
    REJ3 --> NEXT
    ACC --> NEXT
    NEXT --> LOOP

    LOOP -- "Done" --> SAVE{"Any\naccepted?"}:::gate
    SAVE -- "Yes" --> PERSIST["SaveOrdersAsync"]:::db
    SAVE -- "No" --> AUDITSTEP
    PERSIST --> AUDITSTEP["RecordAsync\n(audit entries)"]:::audit
    AUDITSTEP --> RET["Return\nBatchProcessingResult"]:::output

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
