# AdaptiveCash — Technical Interview Coding Challenge

## 🏦 Context

**AdaptiveCash** is an enterprise FinTech platform that automates cash management processes for banks and cash-in-transit companies. The platform handles cash order processing, integrations with banking systems, and client/partner portals.

You are joining the team as a **Full Stack .NET Engineer**. Your first task is to implement a core service for batch processing of cash order requests.

## 📋 The Unified Challenge: Performance & Concurrency

The `CashOrderProcessingService` processes incoming cash orders but occasionally runs into massive batches spanning thousands of orders. The current stub is deeply flawed.

**Target File**: [`CashOrderProcessingService.cs`](src/AdaptiveCash.Application/Services/CashOrderProcessingService.cs)  
**Test File**: [`CashOrderProcessingServiceTests.cs`](tests/AdaptiveCash.Application.Tests/CashOrderProcessingServiceTests.cs)

**Your mission**:
Running `dotnet test` currently reveals **5 failing tests**. You must refactor `ProcessBatchAsync` to resolve them by tackling three distinct computer science problems:

1. **The CPU Time-out (Algorithmic Complexity)**: The Phase 1 "Annihilation" logic uses an $O(N^2)$ algorithm to find offsetting amounts within the batch. For a batch of 50,000 orders, it freezes the entire CPU. Use optimal data structures (like HashMaps/Dictionaries or Two-Pointers) to reduce this to $O(N)$.
2. **The Limit Race Condition**: The daily limit checks are executed concurrently inside a parallel `Task.WhenAll` loop. If a batch contains multiple orders for the *same* client, they will query the database at the exact same time, read the identical starting total, and approve all orders without knowing about each other—completely bypassing the daily limit.
3. **Thread-Safety**: Generic `List<T>` elements are being mutated inside a concurrent parallel loop, throwing thread-safety exceptions or corrupting array counts and breaking audit trails.

Find elegant, enterprise-ready solutions to these constraints so that exactly **5 tests pass** under load!

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

All tests must pass for the component:
- **CashOrderProcessingService**: 5 tests (2 passing examples, 3 failing scenarios)

## 🏗️ Project Structure

```
adaptive-cash-interview/
├── AdaptiveCash.sln
├── src/
│   ├── AdaptiveCash.Domain/          # Models, interfaces, enums
│   │   ├── Models/
│   │   │   ├── CashOrder.cs
│   │   │   └── PaymentResult.cs
│   │   ├── Interfaces/
│   │   │   ├── ICashOrderRepository.cs
│   │   │   ├── IExternalPaymentGateway.cs
│   │   │   └── ICashOrderProcessingService.cs
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
│       └── CashOrderProcessingServiceTests.cs  # All tests in one file (5 failing)
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
