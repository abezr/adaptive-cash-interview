using AdaptiveCash.Domain.Enums;
using AdaptiveCash.Application.Configuration;
using AdaptiveCash.Domain.Interfaces;
using AdaptiveCash.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveCash.Application.Services;

/// <summary>
/// Processes batches of incoming cash order requests.
/// 
/// TODO: Implement the ProcessBatchAsync method according to the acceptance criteria.
/// 
/// Requirements:
///   1. Check against the daily limit per bank client per currency.
///   2. Save accepted orders to the database.
///   3. Return a BatchProcessingResult with accepted and rejected orders.
/// 
/// ⭐ BONUS (Star Challenge):
///   Review the C4 Component diagram (docs/c4/component.md) to discover
///   an additional integration requirement not listed in the basic task.
///   Hint: Look at which components interact with the Order Processing Service.
/// 
/// See: docs/acceptance-criteria.md for the full specification.
/// See: docs/c4/ for architectural diagrams.
/// </summary>
public class CashOrderProcessingService : ICashOrderProcessingService
{
    private readonly ICashOrderRepository _repository;
    private readonly IAuditTrailService _auditTrailService;
    private readonly CashOrderProcessingOptions _options;
    private readonly ILogger<CashOrderProcessingService> _logger;

    public CashOrderProcessingService(
        ICashOrderRepository repository,
        IAuditTrailService auditTrailService,
        CashOrderProcessingOptions options,
        ILogger<CashOrderProcessingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditTrailService = auditTrailService ?? throw new ArgumentNullException(nameof(auditTrailService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BatchProcessingResult> ProcessBatchAsync(
        IEnumerable<CashOrderRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));

        var acceptedOrders = new List<CashOrder>();
        var rejectedOrders = new List<RejectedOrder>();
        var auditEntries = new List<AuditTrailEntry>();
        
        var limitsCache = new Dictionary<(int, string), decimal>();

        foreach (var req in requests)
        {
            // We should really check cancellation here but forgot

            var key = (req.BankClientId, req.Currency);

            if (!limitsCache.TryGetValue(key, out var limit))
            {
                var clientLimit = await _repository.GetClientDailyLimitAsync(req.BankClientId, req.Currency, cancellationToken);
                limit = clientLimit?.MaxDailyAmount ?? _options.DefaultMaxDailyAmount;
                limitsCache[key] = limit;
            }

            // Bug: we query the database every time in the loop but don't accumulate intra-batch running totals!
            var currentTotal = await _repository.GetTotalOrderedTodayAsync(req.BankClientId, req.Currency, DateTime.UtcNow.Date, cancellationToken);

            if (currentTotal + req.Amount > limit)
            {
                rejectedOrders.Add(new RejectedOrder { Request = req, Reason = "Daily limit exceeded" });
                
                auditEntries.Add(new AuditTrailEntry
                {
                    EntityType = "CashOrder",
                    Severity = AuditSeverity.Warning,
                    BankClientId = req.BankClientId,
                    Details = "Order rejected due to daily limit"
                });
                continue;
            }

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
                BankClientId = req.BankClientId,
                Details = "Order accepted"
            });
        }

        if (acceptedOrders.Any())
        {
            await _repository.SaveOrdersAsync(acceptedOrders, cancellationToken);
        }

        if (auditEntries.Any())
        {
            await _auditTrailService.RecordAsync(auditEntries, cancellationToken);
        }

        return new BatchProcessingResult { AcceptedOrders = acceptedOrders, RejectedOrders = rejectedOrders };
    }
}
