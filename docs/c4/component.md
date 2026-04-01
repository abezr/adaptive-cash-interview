# C4 — Level 3: Components (Order Processing Module)

<div align="center">

*How cash order batch processing is orchestrated inside the Web API container*

</div>

---

## Component Diagram

```mermaid
---
config:
  look: handDrawn
---
flowchart TD
    subgraph webApi ["Web API — Order Processing Module"]
        direction TB
        CTRL["📥 Order Controller\nREST endpoints"]:::input
        AUTH["🔐 Auth Middleware\nJWT + tenant resolution"]:::gate
        PROC["⚙️ Order Processing\nService"]:::router
        VALID["✅ Validation\nEngine"]:::agent
        LIMIT["📊 Limit\nService"]:::agent
        REPO["💾 Cash Order\nRepository"]:::db

        CTRL --> AUTH --> PROC
        PROC --> VALID
        PROC --> LIMIT
        PROC --> REPO
        LIMIT --> REPO
    end

    AUDIT["📋 Audit Trail\nService"]:::audit
    PROC -. "Records EVERY\nprocessing decision" .-> AUDIT
    AUDIT --> DB[("💾 Database")]:::db
    REPO --> DB

    TENANT["🏢 Tenant Context\nScoped Service"]:::gate
    AUTH -. "Resolves" .-> TENANT
    PROC -. "Reads" .-> TENANT

    classDef input fill:transparent,stroke:#319795,stroke-width:3px
    classDef gate fill:transparent,stroke:#38A169,stroke-width:3px
    classDef router fill:transparent,stroke:#2B6CB0,stroke-width:3px
    classDef agent fill:transparent,stroke:#E53E50,stroke-width:3px
    classDef db fill:transparent,stroke:#3182CE,stroke-width:3px
    classDef audit fill:transparent,stroke:#DD6B20,stroke-width:3px
```

---

## Components Explained

### Order Controller
Receives batch requests via `POST /api/orders/batch`, delegates to the processing service, returns results.

### Auth Middleware
Extracts JWT, resolves tenant context (BankClientId), enforces role-based access control.

### Order Processing Service
**Core service (your task)**: orchestrates validation, limit checking, persistence, and audit trail recording.

Interface: `ICashOrderProcessingService`

### Validation Engine
Structural and business rule validation for cash orders:
- amount must be greater than zero;
- currency must be in the supported set;
- case-insensitive currency matching.

### Limit Service
Manages and enforces daily limits per client per currency:
- checks client-specific limits from the database;
- falls back to global default when no custom limit exists;
- tracks running totals within a batch.

### Cash Order Repository
Data access for cash orders, limits, and daily totals.

Interface: `ICashOrderRepository`

### Audit Trail Service
**CRITICAL**: Records every processing decision for regulatory compliance.

Interface: `IAuditTrailService`

Every state transition must be recorded with:
- `EntityType` and `EntityId` for traceability;
- `Severity` level (Info for accepted, Warning for rejected);
- `BankClientId` for multi-tenant audit isolation;
- `TimestampUtc` for chronological reconstruction.

### Tenant Context
Scoped service holding the current `BankClientId` resolved from JWT claims.

---

## ⚠️ Key Architectural Constraint

> **The Order Processing Service MUST call the Audit Trail Service for every batch processing operation.**
>
> This is a **regulatory requirement**. In FinTech systems operating with banking institutions, every decision (accept, reject, validate) must be recorded. Failure to record audit entries constitutes a compliance violation.

---

## Data Flow: Batch Order Processing

```mermaid
---
config:
  look: handDrawn
---
sequenceDiagram
    participant C as 📥 Controller
    participant P as ⚙️ Processing Service
    participant V as ✅ Validator
    participant L as 📊 Limit Service
    participant R as 💾 Repository
    participant A as 📋 Audit Trail

    C->>P: ProcessBatchAsync(requests)

    loop For each request
        P->>V: Validate(request)
        V-->>P: ValidationResult

        alt Validation Failed
            P->>P: Add to RejectedOrders
        else Validation Passed
            P->>L: CheckDailyLimit(clientId, currency, amount)
            L->>R: GetTotalOrderedTodayAsync()
            R-->>L: totalToday
            L->>R: GetClientDailyLimitAsync()
            R-->>L: clientLimit or null
            L-->>P: LimitCheckResult

            alt Limit Exceeded
                P->>P: Add to RejectedOrders
            else Within Limit
                P->>P: Add to AcceptedOrders + update running total
            end
        end
    end

    P->>R: SaveOrdersAsync(acceptedOrders)
    P->>A: RecordAsync(auditEntries)
    P-->>C: BatchProcessingResult
```

---

## State Machine: Cash Order Lifecycle

```mermaid
---
config:
  look: handDrawn
---
stateDiagram-v2
    [*] --> Received: Order submitted
    Received --> Validated: Passes all validation
    Received --> Rejected: Fails validation or limit check
    Validated --> Processing: Sent to banking system
    Processing --> Confirmed: Banking system confirms
    Processing --> Failed: Banking system rejects
    Confirmed --> Completed: Settlement finalized
    Failed --> Received: Retry (manual)
    Rejected --> [*]: Terminal state
    Completed --> [*]: Terminal state
```

> **Note**: For this interview task, you implement the `Received → Validated` and `Received → Rejected` transitions. The remaining transitions (Processing, Confirmed, Completed, Failed) are handled by downstream services in the full platform.
