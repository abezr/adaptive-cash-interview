# Acceptance Criteria: Cash Order Processing Service

## Overview

Implement the `CashOrderProcessingService.ProcessBatchAsync()` method in
`src/AdaptiveCash.Application/Services/CashOrderProcessingService.cs`.

All unit tests in `tests/AdaptiveCash.Application.Tests/` must pass.

Run: `dotnet test`

---

## AC-1: Input Validation

### AC-1.1: Amount Validation
- **GIVEN** a cash order request with amount ≤ 0
- **WHEN** the batch is processed
- **THEN** the order is rejected with a non-empty reason string

### AC-1.2: Currency Validation
- **GIVEN** a cash order request with a currency not in `SupportedCurrencies`
- **WHEN** the batch is processed
- **THEN** the order is rejected with a non-empty reason string

### AC-1.3: Currency Case Insensitivity
- **GIVEN** a cash order request with currency "usd" (lowercase)
- **WHEN** "USD" is in the supported currencies set
- **THEN** the order is accepted (currency validation is case-insensitive)

### AC-1.4: Null Input
- **GIVEN** a null value passed as requests
- **WHEN** `ProcessBatchAsync` is called
- **THEN** an `ArgumentNullException` is thrown

---

## AC-2: Daily Limit Enforcement

### AC-2.1: Default Limit
- **GIVEN** no client-specific limit exists (`GetClientDailyLimitAsync` returns null)
- **WHEN** the order amount + already ordered today > `DefaultMaxDailyAmount`
- **THEN** the order is rejected

### AC-2.2: Client-Specific Limit
- **GIVEN** a client-specific limit exists in the database
- **WHEN** limit checking is performed
- **THEN** the client-specific limit is used instead of the default

### AC-2.3: Running Total Within Batch
- **GIVEN** multiple orders from the same client + currency in one batch
- **WHEN** processing sequentially within the batch
- **THEN** earlier accepted orders in the batch count toward the running total for subsequent orders

### AC-2.4: Per-Currency Independence
- **GIVEN** orders from the same client but different currencies
- **WHEN** checking daily limits
- **THEN** each currency has its own independent limit tracking

### AC-2.5: Boundary — Exactly At Limit
- **GIVEN** the already ordered total equals the daily limit
- **WHEN** any new order (even 1 unit) is submitted
- **THEN** the order is rejected

---

## AC-3: Result Construction

### AC-3.1: Accepted Orders
- **GIVEN** an order passes all validations
- **WHEN** the result is constructed
- **THEN** the accepted order has:
  - `Id`: a new, non-empty GUID
  - `Status`: `OrderStatus.Validated`
  - `CreatedAtUtc`: approximately UTC now (within 5 seconds)
  - `RejectionReason`: null
  - All other properties copied from the request

### AC-3.2: Rejected Orders
- **GIVEN** an order fails validation
- **WHEN** the result is constructed
- **THEN** the rejected order contains:
  - The original `CashOrderRequest`
  - A non-empty human-readable `Reason` string

### AC-3.3: Mixed Batches
- **GIVEN** a batch with both valid and invalid orders
- **WHEN** processed
- **THEN** the result correctly separates accepted and rejected orders

### AC-3.4: Empty Batch
- **GIVEN** an empty collection of requests
- **WHEN** processed
- **THEN** the result has empty accepted and rejected lists

---

## AC-4: Persistence

### AC-4.1: Save Accepted Orders
- **GIVEN** at least one order is accepted
- **WHEN** processing completes
- **THEN** `SaveOrdersAsync` is called with the accepted orders

### AC-4.2: No Save for Rejected-Only Batches
- **GIVEN** all orders in a batch are rejected
- **WHEN** processing completes
- **THEN** `SaveOrdersAsync` is NOT called with any orders

---

## ⭐ AC-5: Audit Trail (Star Challenge)

> **Hint**: Review `docs/c4/component.md` — the Component Diagram shows that
> the Order Processing Service has a mandatory connection to the Audit Trail Service.

### AC-5.1: Audit for Accepted Orders
- **GIVEN** orders are accepted
- **WHEN** processing completes
- **THEN** `IAuditTrailService.RecordAsync` is called with entries where:
  - `EntityType` = "CashOrder"
  - `Severity` = `AuditSeverity.Info`

### AC-5.2: Audit for Rejected Orders
- **GIVEN** orders are rejected
- **WHEN** processing completes
- **THEN** `IAuditTrailService.RecordAsync` is called with entries where:
  - `EntityType` = "CashOrder"
  - `Severity` = `AuditSeverity.Warning`

### AC-5.3: Audit Context
- **GIVEN** any order is processed
- **WHEN** audit entries are created
- **THEN** each entry contains the `BankClientId` from the request

### AC-5.4: Audit for Empty Batches
- **GIVEN** an empty batch
- **WHEN** processed
- **THEN** `RecordAsync` is still called (for traceability)

---

## AC-6: Cancellation Support

### AC-6.1: CancellationToken Propagation
- **GIVEN** a cancelled `CancellationToken`
- **WHEN** `ProcessBatchAsync` is called
- **THEN** an `OperationCanceledException` is thrown

---

## Test Summary

| Test Category | Test Count | Tests |
|---------------|-----------|-------|
| Basic Validation | 5 | Empty batch, valid order, zero/negative amount, unsupported currency, case insensitivity |
| Daily Limits | 5 | Exceeded, at limit, below limit, client-specific, default fallback |
| Mixed Batches | 3 | Mixed valid/invalid, running total, per-currency independence |
| Persistence | 3 | Save accepted, no save for rejected, correct properties |
| ⭐ Audit Trail | 4 | Accepted audit, rejected audit, client context, empty batch |
| Edge Cases | 2 | Null input, cancellation |
| **Total** | **22** | |

All 22 tests must pass for a complete implementation.
The first 18 tests (non-star) cover the basic task requirements.
The last 4 tests cover the star challenge (audit trail integration).
