using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Domain.Interfaces;

/// <summary>
/// Service for processing batches of cash order requests.
/// 
/// This is the primary service the candidate must implement.
/// See acceptance-criteria.md for detailed requirements.
/// </summary>
public interface ICashOrderProcessingService
{
    /// <summary>
    /// Processes a batch of incoming cash order requests.
    /// 
    /// For each request:
    /// 1. Validates the request (amount > 0, supported currency).
    /// 2. Checks against the daily limit per bank client per currency.
    /// 3. Saves valid orders to the database.
    /// 4. Returns a result with accepted and rejected orders.
    /// </summary>
    /// <param name="requests">The batch of cash order requests to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result containing accepted and rejected orders.</returns>
    Task<BatchProcessingResult> ProcessBatchAsync(
        IEnumerable<CashOrderRequest> requests,
        CancellationToken cancellationToken = default);
}
