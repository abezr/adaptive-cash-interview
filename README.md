# AdaptiveCash — Technical Interview Coding Challenge

## 🏦 Context

**AdaptiveCash** is an enterprise FinTech platform that automates cash management processes for banks and cash-in-transit companies. The platform handles cash order processing, integrations with banking systems, and client/partner portals.

You are joining the team as a **Full Stack .NET Engineer**. Your first task is to implement a core service for batch processing of cash order requests.

## 📋 Your Task

Implement the `ProcessBatchAsync` method in:

```
src/AdaptiveCash.Application/Services/CashOrderProcessingService.cs
```

### Requirements

1. **Validate** each order request (amount > 0, currency is supported).
2. **Check daily limits** per bank client per currency (client-specific or global default of 500,000).
3. **Track running totals** within the batch (multiple orders from the same client must be cumulative).
4. **Save** valid orders to the database via the repository.
5. **Return** a `BatchProcessingResult` with accepted and rejected orders (with rejection reasons).

### ⭐ Star Challenge (Bonus)

Review the architecture documentation in `docs/c4/` — specifically the **Component Diagram** (`docs/c4/component.md`). There is an additional integration requirement embedded in the architecture that is not listed in the basic task above. Discover it and implement it.

**Hint**: Look at which components the Order Processing Service connects to in the diagram.

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

All **22 unit tests** must pass:
- **18 tests** — basic task requirements
- **4 tests** — ⭐ star challenge (audit trail integration)

## 🏗️ Project Structure

```
adaptive-cash-interview/
├── AdaptiveCash.sln
├── src/
│   ├── AdaptiveCash.Domain/          # Models, interfaces, enums
│   │   ├── Models/
│   │   │   ├── CashOrder.cs          # Order entity and request DTO
│   │   │   ├── BatchProcessingResult.cs
│   │   │   ├── ClientDailyLimit.cs
│   │   │   └── AuditTrailEntry.cs
│   │   ├── Interfaces/
│   │   │   ├── ICashOrderProcessingService.cs
│   │   │   ├── ICashOrderRepository.cs
│   │   │   └── IAuditTrailService.cs
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
│       └── CashOrderProcessingServiceTests.cs  # 22 failing tests
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

- .NET 8
- C# 12
- xUnit + Moq + FluentAssertions (tests)
- No database needed — all dependencies are mocked in tests

Good luck! 🚀
