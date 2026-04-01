namespace AdaptiveCash.Domain.Models;

/// <summary>
/// Result of processing a batch of cash order requests.
/// Contains both accepted and rejected orders with rejection reasons.
/// </summary>
public class BatchProcessingResult
{
    /// <summary>Orders that passed validation and were saved to the database.</summary>
    public List<CashOrder> AcceptedOrders { get; set; } = new();

    /// <summary>Orders that failed validation, each with a rejection reason.</summary>
    public List<RejectedOrder> RejectedOrders { get; set; } = new();

    /// <summary>Total number of orders in the batch.</summary>
    public int TotalCount => AcceptedOrders.Count + RejectedOrders.Count;

    /// <summary>Number of successfully processed orders.</summary>
    public int AcceptedCount => AcceptedOrders.Count;

    /// <summary>Number of rejected orders.</summary>
    public int RejectedCount => RejectedOrders.Count;
}

/// <summary>
/// Represents a rejected order with the reason for rejection.
/// </summary>
public class RejectedOrder
{
    /// <summary>The original request that was rejected.</summary>
    public CashOrderRequest Request { get; set; } = null!;

    /// <summary>Human-readable reason for rejection.</summary>
    public string Reason { get; set; } = string.Empty;
}
