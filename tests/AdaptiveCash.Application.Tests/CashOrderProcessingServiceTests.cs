using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
/// 
/// Contains limited examples of passing tests, and exactly 5 FAILING tests 
/// which you must fix to complete the assignment!
/// Run with: dotnet test
/// </summary>
public class CashOrderProcessingServiceTests
{
    private readonly Mock<ICashOrderRepository> _repositoryMock;
    private readonly Mock<IExternalPaymentGateway> _gatewayMock;
    private readonly Mock<ILogger<CashOrderProcessingService>> _loggerMock;
    private readonly CashOrderProcessingOptions _options;
    private readonly CashOrderProcessingService _sut;

    public CashOrderProcessingServiceTests()
    {
        _repositoryMock = new Mock<ICashOrderRepository>();
        _gatewayMock = new Mock<IExternalPaymentGateway>();
        _gatewayMock.Setup(g => g.ProcessPaymentAsync(It.IsAny<CashOrderRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PaymentResult { IsSuccess = true });
        
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
            _gatewayMock.Object,
            _options,
            _loggerMock.Object);
    }

    // ========================================================================
    // WORKING EXAMPLES (THESE PASS SUCCESSFULLY)
    // Use these to understand how the service interacts with dependencies.
    // ========================================================================

    [Fact]
    public async Task ProcessBatchAsync_WithValidOrder_AcceptsOrder()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 10_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date }
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
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenDailyLimitExceeded_RejectsOrder()
    {
        // Arrange
        var requests = new List<CashOrderRequest>
        {
            new() { BankClientId = 1, Amount = 100_000m, Currency = "USD", RequestedDate = DateTime.UtcNow.Date }
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

    // ========================================================================
    // FAILURES TO FIX (THESE THROW EXCEPTIONS OR TIMEOUT)
    // Your task is to modify the CashOrderProcessingService to make these pass!
    // ========================================================================

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
    public async Task ProcessBatchAsync_WithLargeBatchOfPairs_AnnihilatesEfficientlyWithinTimeLimit()
    {
        // Arrange
        // Generate an extremely large batch where 99% are +100 and -100 pairs from the same client
        int pairCount = 30_000;
        var requests = new List<CashOrderRequest>(pairCount * 2);
        
        for (int i = 0; i < pairCount; i++)
        {
            requests.Add(new CashOrderRequest { BankClientId = 1, Currency = "USD", Amount = 100 });
        }
        for (int i = 0; i < pairCount; i++)
        {
            requests.Add(new CashOrderRequest { BankClientId = 1, Currency = "USD", Amount = -100 });
        }
        // Shuffle or leave ordered. The O(N^2) bug will timeout here regardless.

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        var result = await _sut.ProcessBatchAsync(requests);
        
        sw.Stop();

        // Assert
        result.AcceptedOrders.Should().BeEmpty("All + and - orders should have naturally annihilated each other locally");
        
        // Fail if it took longer than 1.5 seconds (naive O(N^2) takes ~5+ seconds, optimal O(N) takes <0.1 sec)
        sw.ElapsedMilliseconds.Should().BeLessThan(1500, "Algorithm should process annihilation optimally (e.g. O(N) using HashMap/TwoPointers), not O(N^2).");
    }

    [Fact]
    public async Task ProcessBatchAsync_WithHighConcurrency_MaintainsThreadSafetyOnCollections()
    {
        // Arrange
        // We inject simulated network latency so the tasks run heavily concurrent inside WhenAll
        _gatewayMock.Setup(g => g.ProcessPaymentAsync(It.IsAny<CashOrderRequest>(), It.IsAny<CancellationToken>()))
                    .Returns(async () => 
                    {
                        await Task.Delay(2);
                        return new PaymentResult { IsSuccess = true };
                    });

        int orderCount = 5_000;
        var requests = Enumerable.Range(1, orderCount).Select(i => new CashOrderRequest 
        { 
            BankClientId = i, // Spreading across clients avoids DB limit race conditions
            Currency = "USD", 
            Amount = 100 
        }).ToList();

        // Act
        var result = await _sut.ProcessBatchAsync(requests);

        // Assert
        // A thread-unsafe List will throw an exception during WhenAll, or lose items if lucky.
        // If they use ConcurrentBag or lock(), it will exactly match 5000.
        result.AcceptedOrders.Should().HaveCount(orderCount, "Thread unsafe collections drop elements under heavy concurrency.");
    }
}
