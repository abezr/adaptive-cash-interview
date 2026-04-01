using AdaptiveCash.Domain.Enums;

namespace AdaptiveCash.Domain.Models;

/// <summary>
/// Represents an incoming cash order request from a bank client.
/// This is the input DTO — not yet persisted.
/// </summary>
public class CashOrderRequest
{
    /// <summary>Unique identifier of the bank client submitting the order.</summary>
    public int BankClientId { get; set; }

    /// <summary>Requested cash amount. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code (e.g., "USD", "EUR", "UAH").</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Date the cash is requested for.</summary>
    public DateTime RequestedDate { get; set; }
}

/// <summary>
/// Represents a persisted cash order entity.
/// </summary>
public class CashOrder
{
    public Guid Id { get; set; }
    public int BankClientId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}
