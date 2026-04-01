# ADR-001: Cash Order Batch Processing Service Design

| Field          | Value                                      |
|----------------|--------------------------------------------|
| **Status**     | Accepted                                   |
| **Date**       | 2024-06-01                                 |
| **Deciders**   | Platform Architecture Team                 |
| **Context**    | AdaptiveCash Platform — Order Processing   |

## Context

AdaptiveCash is an enterprise FinTech platform that automates cash management processes for banks and cash-in-transit companies. The platform serves multiple bank clients (multi-tenant) and must handle cash order requests that arrive in batches from client portals, partner APIs, and integration endpoints.

We need a service that processes incoming cash order requests with the following constraints:
- **Regulatory compliance**: Every action must be auditable. Financial regulators require full traceability of order processing decisions.
- **Data integrity**: Invalid or over-limit orders must never be persisted as valid.
- **Multi-tenant isolation**: Each bank client's limits and data must be isolated.
- **Performance**: The system must handle batches of hundreds of orders efficiently.

## Decision

### 1. Batch Processing Over Individual Processing

We chose batch processing (`ProcessBatchAsync(IEnumerable<CashOrderRequest>)`) rather than processing orders one at a time because:
- Bank portals and partner APIs typically submit multiple orders at once.
- Batch processing allows us to optimize database round-trips (single `SaveOrdersAsync` call for all accepted orders).
- Within-batch running totals prevent multiple orders from the same client bypassing limits by racing each other.

### 2. Two-Level Daily Limit System

We implemented a two-level limit system:
- **Client-specific limits** stored in the database (`GetClientDailyLimitAsync`), configurable per bank client per currency.
- **Global default limit** from configuration (`CashOrderProcessingOptions.DefaultMaxDailyAmount`), used when no client-specific limit exists.

This allows flexible limit management: new clients start with the default, and sales/operations can configure custom limits without code changes.

### 3. Mandatory Audit Trail Integration

As defined in the C4 Component diagram, the Order Processing Service has a mandatory dependency on the Audit Trail Service. Every processing operation — including empty batches, accepted orders, and rejected orders — must generate audit entries. This is a regulatory requirement for FinTech platforms operating with banking institutions.

Audit entries include:
- `EntityType`: Always "CashOrder" for this service.
- `Severity`:
  - `Info` for accepted orders.
  - `Warning` for rejected orders (limit exceeded, invalid data).
- `BankClientId`: The tenant context of the operation.

### 4. Validation-First, Fail-Fast Approach

Each order goes through validation before limit checking:
1. **Structural validation**: Amount > 0, currency is non-empty and supported.
2. **Business rule validation**: Daily limit check including running total within the batch.

Orders that fail structural validation are immediately rejected without querying the database, reducing unnecessary I/O.

### 5. Running Total Tracking Within Batch

When processing multiple orders from the same client+currency in a single batch, we maintain a running total to prevent limit bypass. Example:
- Client has 400,000 already ordered today, limit is 500,000.
- Batch contains: Order A (50,000) and Order B (60,000).
- Order A: 400,000 + 50,000 = 450,000 ≤ 500,000 → Accepted.
- Order B: 450,000 + 60,000 = 510,000 > 500,000 → Rejected.

Without running total tracking, both orders would pass (400,000 + 50,000 ≤ 500,000 and 400,000 + 60,000 ≤ 500,000), violating the limit.

## Consequences

### Positive
- Atomic batch processing with clear accept/reject separation.
- Full audit trail for regulatory compliance.
- Flexible limit configuration (client-specific overrides).
- Safe within-batch limit enforcement via running totals.

### Negative
- Slightly more complex than simple per-order processing.
- Running total tracking requires careful state management within the method.
- Audit trail dependency adds latency to each processing operation.

### Risks
- **Concurrency**: Two simultaneous batches for the same client could both pass the limit check. This is mitigated at the database level with optimistic concurrency or serializable transactions (out of scope for this service — handled by infrastructure).
- **Scalability**: Very large batches (thousands of orders) may need chunking in the future.

## Alternatives Considered

1. **Per-order processing**: Simpler but doesn't handle within-batch limit enforcement. Rejected.
2. **Queue-based async processing**: Better for very high volumes but adds complexity. Deferred for future iteration.
3. **Database-level validation via triggers**: Would bypass the application layer, making audit trail integration harder. Rejected.
