using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Domain.Interfaces;

/// <summary>
/// Repository for cash order persistence operations.
/// </summary>
public interface ICashOrderRepository
{
    /// <summary>
    /// Returns the total amount ordered today for a specific bank client and currency.
    /// Used for daily limit validation.
    /// </summary>
    /// <param name="bankClientId">The bank client identifier.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="date">The date to check (typically today).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total amount already ordered today for the given client + currency.</returns>
    Task<decimal> GetTotalOrderedTodayAsync(
        int bankClientId,
        string currency,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of cash orders to the database.
    /// </summary>
    /// <param name="orders">The orders to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveOrdersAsync(
        IEnumerable<CashOrder> orders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the client-specific daily limit for a bank client and currency.
    /// Returns null if no custom limit is configured (use global default).
    /// </summary>
    /// <param name="bankClientId">The bank client identifier.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Client-specific limit or null if using global default.</returns>
    Task<ClientDailyLimit?> GetClientDailyLimitAsync(
        int bankClientId,
        string currency,
        CancellationToken cancellationToken = default);
}
