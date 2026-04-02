using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;
using System.Collections.Concurrent;

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
        // Bug: ConcurrentDictionary protects the keys, but the Values are non-thread-safe Lists!
        var groupedResults = new ConcurrentDictionary<int, List<PaymentResult>>();
        int successfulCount = 0;

        var tasks = orders.Select(async order =>
        {
            var result = await _gateway.ProcessPaymentAsync(order, cancellationToken);
            
            // Add to dictionary. 
            // RACE CONDITION: Multiple threads resolving the same List<PaymentResult> 
            // will call .Add() simultaneously, throwing exceptions or losing data.
            var clientList = groupedResults.GetOrAdd(order.BankClientId, _ => new List<PaymentResult>());
            clientList.Add(result); 

            if (result.IsSuccess)
            {
                Interlocked.Increment(ref successfulCount);
            }
        });

        await Task.WhenAll(tasks);

        var allPayments = groupedResults.Values.SelectMany(v => v).ToList();
        return new DispatchResult { SuccessfulCount = successfulCount, CompletedPayments = allPayments };
    }
}
