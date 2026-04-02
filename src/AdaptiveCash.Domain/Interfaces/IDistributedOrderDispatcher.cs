using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Domain.Interfaces;

public class PaymentResult
{
    public Guid OrderId { get; set; }
    public bool IsSuccess { get; set; }
}

public class DispatchResult
{
    public int SuccessfulCount { get; set; }
    public IReadOnlyCollection<PaymentResult> CompletedPayments { get; set; } = new List<PaymentResult>();
}

public interface IExternalPaymentGateway
{
    /// <summary>
    /// Processes a payment externally. This is a network-bound call.
    /// </summary>
    Task<PaymentResult> ProcessPaymentAsync(CashOrder order, CancellationToken cancellationToken = default);
}

public interface IDistributedOrderDispatcher
{
    /// <summary>
    /// Dispatches orders to the external gateway concurrently for performance.
    /// MUST be thread-safe and avoid race conditions.
    /// </summary>
    Task<DispatchResult> DispatchConcurrentlyAsync(IEnumerable<CashOrder> orders, CancellationToken cancellationToken = default);
}
