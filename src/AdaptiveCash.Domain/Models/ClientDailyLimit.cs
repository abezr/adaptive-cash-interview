namespace AdaptiveCash.Domain.Models;

/// <summary>
/// Represents a client-specific daily limit configuration.
/// Loaded from the database; each bank client may have different limits per currency.
/// </summary>
public class ClientDailyLimit
{
    public int BankClientId { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>Maximum allowed amount per day for this client + currency combination.</summary>
    public decimal MaxDailyAmount { get; set; }
}
