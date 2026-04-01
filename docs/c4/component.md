# C4 Model: Component Diagram

## Web API — Component Diagram

This diagram shows the internal components of the Web API container, focusing on the Order Processing module.

```mermaid
C4Component
    title AdaptiveCash Web API — Component Diagram (Order Processing Module)

    Container_Boundary(webApi, "Web API") {
        Component(orderController, "Order Controller", "ASP.NET Core Controller", "REST endpoints for order submission and management")
        Component(authMiddleware, "Auth Middleware", "ASP.NET Core Middleware", "JWT authentication, tenant context resolution, role-based access")
        Component(orderProcessingService, "Order Processing Service", "C# Service", "Core business logic: validates orders, checks limits, processes batches")
        Component(validationEngine, "Validation Engine", "C# Component", "Structural and business rule validation for cash orders")
        Component(limitService, "Limit Service", "C# Component", "Manages and enforces daily limits per client per currency")
        Component(auditTrailService, "Audit Trail Service", "C# Service", "Records ALL processing decisions for regulatory compliance. MANDATORY for every state transition.")
        Component(cashOrderRepository, "Cash Order Repository", "C# Repository", "Data access for cash orders, limits, and daily totals")
        Component(tenantContext, "Tenant Context", "Scoped Service", "Holds current tenant (BankClientId) resolved from JWT claims")
    }

    ContainerDb(database, "Database", "MS SQL / Oracle")
    System_Ext(bankCore, "Core Banking System", "Order confirmation")

    Rel(orderController, authMiddleware, "Passes through")
    Rel(orderController, orderProcessingService, "Delegates batch processing")
    Rel(orderProcessingService, validationEngine, "Validates each order")
    Rel(orderProcessingService, limitService, "Checks daily limits")
    Rel(orderProcessingService, cashOrderRepository, "Saves accepted orders")
    Rel(orderProcessingService, auditTrailService, "Records audit entries for EVERY processed order (accepted AND rejected)")
    Rel(limitService, cashOrderRepository, "Queries daily totals and client limits")
    Rel(auditTrailService, database, "Persists audit entries")
    Rel(cashOrderRepository, database, "Reads/Writes order data")
    Rel(orderProcessingService, bankCore, "Confirms orders (future phase)")
```

## Component Descriptions

| Component | Interface | Responsibility |
|-----------|-----------|----------------|
| **Order Controller** | `POST /api/orders/batch` | Receives batch requests, delegates to processing service, returns results |
| **Auth Middleware** | n/a | Extracts JWT, resolves tenant context, enforces RBAC |
| **Order Processing Service** | `ICashOrderProcessingService` | **Core service (your task)**: orchestrates validation, limit checking, persistence, and audit trail recording |
| **Validation Engine** | Internal | Validates: amount > 0, currency supported, request date valid |
| **Limit Service** | Internal | Checks client-specific and default daily limits with running total tracking |
| **Audit Trail Service** | `IAuditTrailService` | **CRITICAL**: Records every processing decision. Every accepted order → `Info` entry. Every rejected order → `Warning` entry. Every batch → at minimum one audit call. |
| **Cash Order Repository** | `ICashOrderRepository` | CRUD operations for orders, queries for daily totals and client limits |
| **Tenant Context** | Scoped service | Provides `BankClientId` for the current request scope |

## ⚠️ Key Architectural Constraint

> **The Order Processing Service MUST call the Audit Trail Service for every batch processing operation.**
>
> This is a **regulatory requirement**. In FinTech systems operating with banking institutions, every decision (accept, reject, validate) must be recorded with:
> - **Entity type** and **entity ID** for traceability
> - **Severity level** (Info for accepted, Warning for rejected)
> - **Bank client context** for multi-tenant audit isolation
> - **Timestamp** for chronological audit reconstruction
>
> Failure to record audit entries constitutes a compliance violation.

## Data Flow: Batch Order Processing

```mermaid
sequenceDiagram
    participant C as Order Controller
    participant P as Order Processing Service
    participant V as Validation Engine
    participant L as Limit Service
    participant R as Cash Order Repository
    participant A as Audit Trail Service
    participant DB as Database

    C->>P: ProcessBatchAsync(requests)
    
    loop For each request
        P->>V: Validate(request)
        V-->>P: ValidationResult
        
        alt Validation Failed
            P->>P: Add to RejectedOrders
        else Validation Passed
            P->>L: CheckDailyLimit(clientId, currency, amount)
            L->>R: GetTotalOrderedTodayAsync()
            R->>DB: SELECT SUM(Amount)
            DB-->>R: totalToday
            R-->>L: totalToday
            L->>R: GetClientDailyLimitAsync()
            R->>DB: SELECT MaxDailyAmount
            DB-->>R: clientLimit (or null)
            R-->>L: clientLimit
            L-->>P: LimitCheckResult
            
            alt Limit Exceeded
                P->>P: Add to RejectedOrders
            else Within Limit
                P->>P: Add to AcceptedOrders, update running total
            end
        end
    end
    
    P->>R: SaveOrdersAsync(acceptedOrders)
    R->>DB: INSERT accepted orders
    
    P->>A: RecordAsync(auditEntries)
    A->>DB: INSERT audit trail entries
    
    P-->>C: BatchProcessingResult
```

## State Machine: Cash Order Lifecycle

```mermaid
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
