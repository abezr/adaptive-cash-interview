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
    private readonly IAuditTrailService _auditTrailService;
    private readonly IExternalPaymentGateway _gateway;
    private readonly CashOrderProcessingOptions _options;
    private readonly ILogger<CashOrderProcessingService> _logger;

    public CashOrderProcessingService(
        ICashOrderRepository repository,
        IAuditTrailService auditTrailService,
        IExternalPaymentGateway gateway,
        CashOrderProcessingOptions options,
        ILogger<CashOrderProcessingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditTrailService = auditTrailService ?? throw new ArgumentNullException(nameof(auditTrailService));
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
        var toRemove = new HashSet<CashOrderRequest>();
        for (int i = 0; i < requestList.Count; i++)
        {
            if (toRemove.Contains(requestList[i])) continue;
            for (int j = i + 1; j < requestList.Count; j++)
            {
                if (!toRemove.Contains(requestList[j]) && 
                    requestList[i].BankClientId == requestList[j].BankClientId &&
                    requestList[i].Currency == requestList[j].Currency &&
                    requestList[i].Amount == -requestList[j].Amount)
                {
                    toRemove.Add(requestList[i]);
                    toRemove.Add(requestList[j]);
                    break;
                }
            }
        }

        var validRequests = requestList.Where(r => !toRemove.Contains(r)).ToList();

        // ------------------------------------------------------------------------------------
        // PHASE 2: LIMIT CHECKING & EXTERNAL DISPATCH
        // Requirement: Enforce daily limits and immediately dispatch accepted orders concurrently.
        // BUG: Thread-unsafe collections used inside Task.WhenAll.
        // BUG: Limit Race Condition - intra-batch accumulation is ignored due to concurrency!
        // ------------------------------------------------------------------------------------
        var acceptedOrders = new List<CashOrder>(); // Thread unsafe!
        var rejectedOrders = new List<RejectedOrder>(); // Thread unsafe!
        var auditEntries = new List<AuditTrailEntry>(); // Thread unsafe!

        var tasks = validRequests.Select(async req => 
        {
            var limitObj = await _repository.GetClientDailyLimitAsync(req.BankClientId, req.Currency, cancellationToken);
            decimal limit = limitObj?.MaxDailyAmount ?? _options.DefaultMaxDailyAmount;
            
            // Limit Race Condition: Evaluates without accumulating intra-batch sums!
            decimal runningTotal = await _repository.GetTotalOrderedTodayAsync(req.BankClientId, req.Currency, DateTime.UtcNow.Date, cancellationToken);

            if (runningTotal + req.Amount > limit)
            {
                rejectedOrders.Add(new RejectedOrder { Request = req, Reason = "Daily limit exceeded" });
                return;
            }

            // Dispatch to External Gateway (simulated Network I/O)
            var paymentResult = await _gateway.ProcessPaymentAsync(req, cancellationToken);

            if (paymentResult.IsSuccess)
            {
                var newOrder = new CashOrder
                {
                    Id = Guid.NewGuid(),
                    BankClientId = req.BankClientId,
                    Amount = req.Amount,
                    Currency = req.Currency,
                    RequestedDate = req.RequestedDate,
                    CreatedAtUtc = DateTime.UtcNow,
                    Status = OrderStatus.Validated
                };
                
                acceptedOrders.Add(newOrder); 
                
                auditEntries.Add(new AuditTrailEntry 
                { 
                    EntityType = "CashOrder", 
                    Severity = AuditSeverity.Info, 
                    BankClientId = newOrder.BankClientId, 
                    Details = "Order accepted and dispatched" 
                });
            }
        });

        await Task.WhenAll(tasks);

        if (acceptedOrders.Any())
        {
            await _repository.SaveOrdersAsync(acceptedOrders, cancellationToken);
        }

        if (auditEntries.Any())
        {
            await _auditTrailService.RecordAsync(auditEntries, cancellationToken);
        }

        return new BatchProcessingResult { AcceptedOrders = acceptedOrders.ToList(), RejectedOrders = rejectedOrders.ToList() };
    }
}
