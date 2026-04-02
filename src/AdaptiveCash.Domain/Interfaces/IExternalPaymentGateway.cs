using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Domain.Interfaces;

public interface IExternalPaymentGateway
{
    /// <summary>
    /// Processes a payment externally. This is a network-bound call.
    /// </summary>
    Task<PaymentResult> ProcessPaymentAsync(CashOrderRequest order, CancellationToken cancellationToken = default);
}
