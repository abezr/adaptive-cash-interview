using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Application.Services;

public class DistributedOrderDispatcher : IDistributedOrderDispatcher
{
    private readonly IExternalPaymentGateway _gateway;

    public DistributedOrderDispatcher(IExternalPaymentGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<DispatchResult> DispatchConcurrentlyAsync(
        IEnumerable<CashOrder> orders, 
        CancellationToken cancellationToken = default)
    {
        // BUG: These state variables are not thread-safe 
        // when accessed from parallel concurrent tasks!
        var completedPayments = new List<PaymentResult>(); 
        int successfulCount = 0; 

        var tasks = orders.Select(async order =>
        {
            var result = await _gateway.ProcessPaymentAsync(order, cancellationToken);
            
            // Race condition: concurrent adds will lose data or throw exceptions
            completedPayments.Add(result); 
            
            if (result.IsSuccess)
            {
                // Race condition: concurrent increments will clobber each other
                successfulCount++; 
            }
        });

        // Wait for all concurrent network calls to finish
        await Task.WhenAll(tasks);

        return new DispatchResult 
        { 
            SuccessfulCount = successfulCount, 
            CompletedPayments = completedPayments 
        };
    }
}
