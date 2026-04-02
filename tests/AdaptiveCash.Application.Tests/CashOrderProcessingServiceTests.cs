using System.Threading.Tasks;
using Xunit;
using AdaptiveCash.Application.Configuration;
using AdaptiveCash.Application.Services;
using AdaptiveCash.Domain.Enums;
using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AdaptiveCash.Application.Tests;

/// <summary>
/// Unit tests for CashOrderProcessingService.
/// These tests define the expected behavior — they will FAIL until the service is implemented.
/// 
/// Run with: dotnet test
/// </summary>
public class CashOrderProcessingServiceTests
{
    private readonly Mock<ICashOrderRepository> _repositoryMock;
    private readonly Mock<IAuditTrailService> _auditTrailServiceMock;
    private readonly Mock<ILogger<CashOrderProcessingService>> _loggerMock;
    private readonly CashOrderProcessingOptions _options;
    private readonly CashOrderProcessingService _sut;

    public CashOrderProcessingServiceTests()
    {
        _repositoryMock = new Mock<ICashOrderRepository>();
        _auditTrailServiceMock = new Mock<IAuditTrailService>();
        _loggerMock = new Mock<ILogger<CashOrderProcessingService>>();
        _options = new CashOrderProcessingOptions
        {
            DefaultMaxDailyAmount = 500_000m,
            SupportedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "USD", "EUR", "UAH", "GBP", "CHF", "PLN"
            }
        };

        _sut = new CashOrderProcessingService(
            _repositoryMock.Object,
            _auditTrailServiceMock.Object,
            _options,
            _loggerMock.Object);
    }

    // ========================================================================
    // BASIC VALIDATION TESTS
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_WithEmptyBatch_ReturnsEmptyResult()
    {
        // Arrange
        var requests = Enumerable.Empty<CashOrderRequest>();

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.Should().NotBeNull();
        result.AcceptedOrders.Should().BeEmpty();
        result.RejectedOrders.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithValidOrder_AcceptsOrder()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 1,
                Amount = 10_000m,
                Currency = "USD",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(1);
        result.RejectedOrders.Should().BeEmpty();

        var accepted = result.AcceptedOrders[0];
        accepted.BankClientId.Should().Be(1);
        accepted.Amount.Should().Be(10_000m);
        accepted.Currency.Should().Be("USD");
        accepted.Status.Should().Be(OrderStatus.Validated);
    }



    // ========================================================================
    // DAILY LIMIT TESTS
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_WhenDailyLimitExceeded_RejectsOrder()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 1,
                Amount = 100_000m,
                Currency = "USD",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        // Already ordered 450,000 today; adding 100,000 would exceed 500,000 limit
        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(450_000m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().BeEmpty();
        result.RejectedOrders.Should().HaveCount(1);
        result.RejectedOrders[0].Reason.Should().Contain("limit", Exactly.Once());
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenExactlyAtLimit_RejectsOrder()
    {
        // Arrange — already at 500,000; any new order should be rejected
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 1,
                Amount = 1m,
                Currency = "EUR",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "EUR", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500_000m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().BeEmpty();
        result.RejectedOrders.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenBelowLimit_AcceptsOrder()
    {
        // Arrange — 400,000 already ordered; adding 50,000 = 450,000 < 500,000
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 1,
                Amount = 50_000m,
                Currency = "USD",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(400_000m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessBatchAsync_UsesClientSpecificLimit_WhenConfigured()
    {
        // Arrange — client has a custom limit of 1,000,000
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 42,
                Amount = 600_000m,
                Currency = "EUR",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(42, "EUR", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(200_000m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(42, "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientDailyLimit
            {
                BankClientId = 42,
                Currency = "EUR",
                MaxDailyAmount = 1_000_000m
            });

        // Act — 200,000 + 600,000 = 800,000 < 1,000,000 custom limit
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessBatchAsync_UsesDefaultLimit_WhenNoClientSpecificLimit()
    {
        // Arrange — no client-specific limit, so default 500,000 applies
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 99,
                Amount = 600_000m,
                Currency = "UAH",
                RequestedDate = DateTime.UtcNow.Date
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(99, "UAH", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(99, "UAH", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act — 600,000 > 500,000 default limit
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().BeEmpty();
        result.RejectedOrders.Should().HaveCount(1);
    }

    // ========================================================================
    // MIXED BATCH TESTS
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_MixedBatch_CorrectlySeparatesAcceptedAndRejected()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },   // valid, below 500k
            new() { BankClientId = 2, Amount = 600_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },   // invalid: exceeds limit
            new() { BankClientId = 3, Amount = 800_000m, Currency = "BTC", RequestedDate = DateTime.UtcNow.Date },   // invalid: exceeds limit
            new() { BankClientId = 1, Amount = 30_000m, Currency = "EUR", RequestedDate = DateTime.UtcNow.Date },   // valid, below 500k
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(2);
        result.RejectedOrders.Should().HaveCount(2);
        result.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task ProcessBatchAsync_MultipleOrdersSameClient_TracksRunningTotalWithinBatch()
    {
        // Arrange — two orders from the same client in the same batch
        // Already ordered: 400,000. First order: 50,000 (ok, total=450,000).
        // Second order: 60,000 (450,000+60,000=510,000 > 500,000 — reject).
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 50_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
            new() { BankClientId = 1, Amount = 60_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(400_000m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(1);
        result.AcceptedOrders[0].Amount.Should().Be(50_000m);
        result.RejectedOrders.Should().HaveCount(1);
        result.RejectedOrders[0].Request.Amount.Should().Be(60_000m);
    }

    [Fact]
    public async Task ProcessBatchAsync_DifferentCurrencies_TrackLimitsIndependently()
    {
        // Arrange — same client, different currencies; each currency has its own limit
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 400_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
            new() { BankClientId = 1, Amount = 400_000m, Currency = "EUR", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert — both should be accepted since limits are per-currency
        result.AcceptedOrders.Should().HaveCount(2);
    }

    // ========================================================================
    // PERSISTENCE TESTS
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_SavesAcceptedOrders_ToRepository()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        await _sut.ProcessBatchAsync(requests);

        // Assert — verify SaveOrdersAsync was called with the accepted orders
        _repositoryMock.Verify(
            r => r.SaveOrdersAsync(
                It.Is<IEnumerable<CashOrder>>(orders => orders.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_DoesNotSaveRejectedOrders()
    {
        // Arrange — all orders exceed limit
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 600_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
            new() { BankClientId = 2, Amount = 800_000m, Currency = "BTC", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert — SaveOrdersAsync should not be called (or called with empty)
        _repositoryMock.Verify(
            r => r.SaveOrdersAsync(
                It.Is<IEnumerable<CashOrder>>(orders => orders.Any()),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_AcceptedOrders_HaveCorrectProperties()
    {
        // Arrange
        var requestDate = new DateTime(2024, 6, 15);
        var requests = new List<CashOrderRequest>
        {
            new()
            {
                BankClientId = 7,
                Amount = 25_000m,
                Currency = "GBP",
                RequestedDate = requestDate
            }
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(7, "GBP", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(7, "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        var order = result.AcceptedOrders.Should().ContainSingle().Subject;
        order.Id.Should().NotBe(Guid.Empty);
        order.BankClientId.Should().Be(7);
        order.Amount.Should().Be(25_000m);
        order.Currency.Should().Be("GBP");
        order.RequestedDate.Should().Be(requestDate);
        order.Status.Should().Be(OrderStatus.Validated);
        order.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        order.RejectionReason.Should().BeNull();
    }

    // ========================================================================
    // ⭐ STAR CHALLENGE: AUDIT TRAIL TESTS
    // These tests verify the requirement from the C4 Component diagram.
    // The candidate must read the diagram to understand that audit trail
    // recording is mandatory for every batch processing operation.
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_RecordsAuditTrail_ForAcceptedOrders()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
            new() { BankClientId = 2, Amount = 20_000m, Currency = "EUR", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        await _sut.ProcessBatchAsync(requests);

        // Assert — audit trail must be called with entries for accepted orders
        _auditTrailServiceMock.Verify(
            a => a.RecordAsync(
                It.Is<IEnumerable<AuditTrailEntry>>(entries =>
                    entries.Count() >= 2 &&
                    entries.All(e => e.EntityType == "CashOrder" && e.Severity == AuditSeverity.Info)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_RecordsAuditTrail_ForRejectedOrders()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 600_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        await _sut.ProcessBatchAsync(requests);

        // Assert — rejections must also be audited with Warning severity
        _auditTrailServiceMock.Verify(
            a => a.RecordAsync(
                It.Is<IEnumerable<AuditTrailEntry>>(entries =>
                    entries.Any(e =>
                        e.EntityType == "CashOrder" &&
                        e.Severity == AuditSeverity.Warning)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_AuditEntries_ContainBankClientId()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 42, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(42, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(42, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        await _sut.ProcessBatchAsync(requests);

        // Assert — audit entries must carry the bank client context
        _auditTrailServiceMock.Verify(
            a => a.RecordAsync(
                It.Is<IEnumerable<AuditTrailEntry>>(entries =>
                    entries.All(e => e.BankClientId == 42)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyBatch_StillRecordsAuditTrail()
    {
        // Arrange
        var requests = Enumerable.Empty<CashOrderRequest>();

        // Act
        await _sut.ProcessBatchAsync(requests);

        // Assert — even empty batches should be audited for traceability
        _auditTrailServiceMock.Verify(
            a => a.RecordAsync(
                It.IsAny<IEnumerable<AuditTrailEntry>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ========================================================================
    // EDGE CASE TESTS
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_WithNullRequests_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ProcessBatchAsync(null!));
    }

    [Fact]
    public async Task ProcessBatchAsync_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ProcessBatchAsync(requests, cts.Token));
    }

    [Fact]
    public async Task ProcessBatchAsync_LargeValidAmount_IsAccepted()
    {
        // Arrange — amount of 499,999.99 with 0 already ordered; should be accepted
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 499_999.99m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date },
        };

        _repositoryMock
            .Setup(r => r.GetTotalOrderedTodayAsync(1, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _repositoryMock
            .Setup(r => r.GetClientDailyLimitAsync(1, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientDailyLimit?)null);

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        result.AcceptedOrders.Should().HaveCount(1);
    }
}
