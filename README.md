# AdaptiveCash — Technical Interview Coding Challenge

## 🏦 Context

**AdaptiveCash** is an enterprise FinTech platform that automates cash management processes for banks and cash-in-transit companies. The platform handles cash order processing, integrations with banking systems, and client/partner portals.

You are joining the team as a **Full Stack .NET Engineer**. Your first task is to implement a core service for batch processing of cash order requests.

## 📋 Challenge 1: Debug Limit Checking

The `CashOrderProcessingService` is **mostly implemented**, but unit tests fail due to logical tracking errors.

**Target File**: [`CashOrderProcessingService.cs`](src/AdaptiveCash.Application/Services/CashOrderProcessingService.cs)  
**Test File**: [`CashOrderProcessingServiceTests.cs`](tests/AdaptiveCash.Application.Tests/CashOrderProcessingServiceTests.cs)

**Your mission**:
1. Run the tests.
2. Fix the bugs inside `ProcessBatchAsync` so all **20 tests** pass (specifically around intra-batch daily limit tracking and empty batch operations).

## 🚀 Challenge 2: Distributed Concurrency

A new requirement asks us to dispatch orders to a distributed system concurrently.

**Target File**: [`DistributedOrderDispatcher.cs`](src/AdaptiveCash.Application/Services/DistributedOrderDispatcher.cs)  
**Test File**: [`DistributedOrderDispatcherTests.cs`](tests/AdaptiveCash.Application.Tests/DistributedOrderDispatcherTests.cs)

**Your mission**:
1. Inspect the `DispatchConcurrentlyAsync` method. 
2. Notice that test `DistributedOrderDispatcherTests` occasionally fails and throws `InvalidOperationException` or drifts counts due to thread-safety issues.
3. Refactor it to be fully thread-safe and performant.

## 📖 Documentation

| Document | Path | Description |
|----------|------|-------------|
| **Acceptance Criteria** | `docs/acceptance-criteria.md` | Full specification in Gherkin-style |
| **ADR** | `docs/adr/001-cash-order-processing-service.md` | Architecture decision record |
| **C4 Context** | `docs/c4/context.md` | System context diagram |
| **C4 Container** | `docs/c4/container.md` | Container diagram |
| **C4 Component** | `docs/c4/component.md` | Component diagram (**read this for ⭐**) |
| **C4 Code** | `docs/c4/code.md` | Code-level class diagram and algorithm |

## 🧪 Running Tests

```bash
dotnet test
```

All tests must pass for both components:
- **CashOrderProcessingService**: 20 tests (16 basic, 4 audit trail)
- **DistributedOrderDispatcher**: 1 threading test

## 🏗️ Project Structure

```
adaptive-cash-interview/
├── AdaptiveCash.sln
├── src/
│   ├── AdaptiveCash.Domain/          # Models, interfaces, enums
│   │   ├── Models/
│   │   │   ├── CashOrder.cs
│   │   │   └── PaymentResult.cs      # Used for concurrency task
│   │   ├── Interfaces/
│   │   │   ├── ICashOrderRepository.cs
│   │   │   └── IDistributedOrderDispatcher.cs
│   │   └── Enums/
│   │       ├── OrderStatus.cs
│   │       └── AuditSeverity.cs
│   ├── AdaptiveCash.Application/     # Business logic (YOUR CODE HERE)
│   │   ├── Configuration/
│   │   │   └── CashOrderProcessingOptions.cs
│   │   └── Services/
│   │       └── CashOrderProcessingService.cs  ← IMPLEMENT THIS
│   └── AdaptiveCash.Infrastructure/  # (stub — not needed for this task)
├── tests/
│   └── AdaptiveCash.Application.Tests/
│       ├── CashOrderProcessingServiceTests.cs  # 2 failing tests (bugs to fix)
│       └── DistributedOrderDispatcherTests.cs  # 1 failing thread-safety test
├── docs/
│   ├── acceptance-criteria.md
│   ├── adr/
│   │   └── 001-cash-order-processing-service.md
│   └── c4/
│       ├── context.md
│       ├── container.md
│       ├── component.md              ← READ THIS FOR ⭐
│       └── code.md
└── frontend-challenge/               # Optional frontend task
    ├── README.md
    └── mockups/
```

## ⏱️ Time

- **Basic task**: ~15 minutes
- **⭐ Star challenge**: included in the same 15 minutes (requires reading C4 diagrams)

## 🎨 Optional: Frontend Challenge

If time permits, you may spend remaining time on the frontend challenge described in `frontend-challenge/README.md`. You may use AI tools for this part.

## 🛠️ Tech Stack

- .NET 10
- C# 13
- xUnit + Moq + FluentAssertions (tests)
- No database needed — all dependencies are mocked in tests

Good luck! 🚀
