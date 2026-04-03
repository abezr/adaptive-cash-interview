using AdaptiveCash.Domain.Enums;
using AdaptiveCash.Application.Configuration;
using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveCash.Application.Services;

/// <summary>
/// Processes batches of incoming cash order requests.
/// </summary>
public class CashOrderProcessingService : ICashOrderProcessingService
{
    private readonly ICashOrderRepository _repository;
    private readonly IExternalPaymentGateway _gateway;
    private readonly CashOrderProcessingOptions _options;
    private readonly ILogger<CashOrderProcessingService> _logger;

    public CashOrderProcessingService(
        ICashOrderRepository repository,
        IExternalPaymentGateway gateway,
        CashOrderProcessingOptions options,
        ILogger<CashOrderProcessingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BatchProcessingResult> ProcessBatchAsync(
        IEnumerable<CashOrderRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));

        var requestList = requests.ToList();
        
        // ------------------------------------------------------------------------------------
        // PHASE 1: ANNIHILATION
        // Requirement: Filter out 'offsetting pairs' within the batch before checking limits.
        // An offsetting pair is any two requests from the same client where req1.Amount == -req2.Amount
        // and req1.Currency == req2.Currency. They annihilate each other and should be skipped.
        // BUG: O(N^2) naive approach causing massive CPU lag on large batches!
        // ------------------------------------------------------------------------------------
        var validRequests = new List<CashOrderRequest>();
        var map = new Dictionary<(int, string, decimal), Stack<CashOrderRequest>>();

        foreach (var req in requestList)
        {
            var oppKey = (req.BankClientId, req.Currency, -req.Amount);

            if (map.TryGetValue(oppKey, out var stack) && stack.Count > 0)
            {
                stack.Pop(); // Annihilated
            }
            else
            {
                var key = (req.BankClientId, req.Currency, req.Amount);
                if (!map.ContainsKey(key)) map[key] = new Stack<CashOrderRequest>();
                map[key].Push(req);
            }
        }

        validRequests = map.Values.SelectMany(s => s).ToList();

        // ------------------------------------------------------------------------------------
        // PHASE 2: LIMIT CHECKING & EXTERNAL DISPATCH
        // Requirement: Enforce daily limits and immediately dispatch accepted orders concurrently.
        // BUG: Thread-unsafe collections used inside Task.WhenAll.
        // BUG: Limit Race Condition - intra-batch accumulation is ignored due to concurrency!
        // ------------------------------------------------------------------------------------
        var acceptedOrders = new System.Collections.Concurrent.ConcurrentBag<CashOrder>(); 
        var rejectedOrders = new System.Collections.Concurrent.ConcurrentBag<RejectedOrder>(); 
        
        var toDispatch = new List<CashOrderRequest>();

        // Safely determine limits sequentially by client to avoid race conditions
        foreach(var group in validRequests.GroupBy(r => new { r.BankClientId, r.Currency }))
        {
            var limitObj = await _repository.GetClientDailyLimitAsync(group.Key.BankClientId, group.Key.Currency, cancellationToken);
            decimal limit = limitObj?.MaxDailyAmount ?? _options.DefaultMaxDailyAmount;
            
            decimal runningTotal = await _repository.GetTotalOrderedTodayAsync(group.Key.BankClientId, group.Key.Currency, DateTime.UtcNow.Date, cancellationToken);

            foreach(var req in group) 
            {
                if (runningTotal + req.Amount > limit)
                {
                    rejectedOrders.Add(new RejectedOrder { Request = req, Reason = "Daily limit exceeded" });
                }
                else
                {
                    runningTotal += req.Amount; // Track dynamically!
                    toDispatch.Add(req);
                }
            }
        }

        var tasks = toDispatch.Select(async req => 
        {
            var paymentResult = await _gateway.ProcessPaymentAsync(req, cancellationToken);

            if (paymentResult.IsSuccess)
            {
                var newOrder = new CashOrder
                {
                    Id = Guid.NewGuid(), BankClientId = req.BankClientId, Amount = req.Amount, Currency = req.Currency,
                    RequestedDate = req.RequestedDate, CreatedAtUtc = DateTime.UtcNow, Status = OrderStatus.Validated
                };
                acceptedOrders.Add(newOrder); 
            }
        });

        await Task.WhenAll(tasks);

        if (acceptedOrders.Any())
        {
            await _repository.SaveOrdersAsync(acceptedOrders, cancellationToken);
        }

        return new BatchProcessingResult { AcceptedOrders = acceptedOrders.ToList(), RejectedOrders = rejectedOrders.ToList() };
    }
}
