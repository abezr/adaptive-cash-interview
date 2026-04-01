using AdaptiveCash.Domain.Models;

namespace AdaptiveCash.Domain.Interfaces;

/// <summary>
/// Service responsible for recording audit trail entries.
/// 
/// IMPORTANT: As shown in the C4 Component diagram, every state transition
/// of a CashOrder MUST be recorded in the audit trail for regulatory compliance.
/// This is a critical requirement for FinTech systems — all order processing,
/// validation outcomes, and status changes must be auditable.
/// 
/// See: docs/c4/component.md — "Audit Trail Service" component.
/// </summary>
public interface IAuditTrailService
{
    /// <summary>
    /// Records an audit trail entry for a processed order batch.
    /// Should be called after each batch processing operation.
    /// </summary>
    /// <param name="entries">Audit entries to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        IEnumerable<AuditTrailEntry> entries,
        CancellationToken cancellationToken = default);
}
