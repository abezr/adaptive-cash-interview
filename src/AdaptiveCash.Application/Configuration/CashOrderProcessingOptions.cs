namespace AdaptiveCash.Application.Configuration;

/// <summary>
/// Configuration for cash order processing.
/// Contains the global default daily limit and supported currencies.
/// </summary>
public class CashOrderProcessingOptions
{
    /// <summary>
    /// Global default maximum daily amount per bank client per currency.
    /// Used when no client-specific limit is configured.
    /// </summary>
    public decimal DefaultMaxDailyAmount { get; set; } = 500_000m;

    /// <summary>
    /// Set of supported ISO 4217 currency codes.
    /// Orders with unsupported currencies will be rejected.
    /// </summary>
    public HashSet<string> SupportedCurrencies { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "UAH", "GBP", "CHF", "PLN"
    };
}
