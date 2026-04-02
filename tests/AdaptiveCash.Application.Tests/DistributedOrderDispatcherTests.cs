using AdaptiveCash.Application.Services;
using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace AdaptiveCash.Application.Tests;

public class DistributedOrderDispatcherTests
{
    private readonly Mock<IExternalPaymentGateway> _gatewayMock;
    private readonly DistributedOrderDispatcher _sut;

    public DistributedOrderDispatcherTests()
    {
        _gatewayMock = new Mock<IExternalPaymentGateway>();
        _sut = new DistributedOrderDispatcher(_gatewayMock.Object);
    }

    [Fact]
    public async Task DispatchConcurrentlyAsync_WithManyConcurrentOrders_DoesNotSufferRaceConditions()
    {
        // Arrange
        int totalOrders = 10000;
        var orders = Enumerable.Range(1, totalOrders).Select(i => new CashOrder
        {
            Id = Guid.NewGuid(),
            Amount = 100m
        }).ToList();

        _gatewayMock
            .Setup(g => g.ProcessPaymentAsync(It.IsAny<CashOrder>(), It.IsAny<CancellationToken>()))
            .Returns(async (CashOrder order, CancellationToken ct) =>
            {
                await Task.Delay(10); // Real delay to force overlapping I/O task completions
                return new PaymentResult { OrderId = order.Id, IsSuccess = true };
            });

        // Act
        // This will likely throw an InvalidOperationException "Collection was modified" 
        // or the counts will mismatch if race conditions occur.
        var result = await _sut.DispatchConcurrentlyAsync(orders);

        // Assert
        result.Should().NotBeNull();
        
        // Assert thread-safety of the collection
        result.CompletedPayments.Should().HaveCount(totalOrders);
        
        // Assert thread-safety of the counter
        result.SuccessfulCount.Should().Be(totalOrders);
    }
}
