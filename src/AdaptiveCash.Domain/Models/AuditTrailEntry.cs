using AdaptiveCash.Domain.Enums;

namespace AdaptiveCash.Domain.Models;

/// <summary>
/// Represents an audit trail entry for compliance and monitoring.
/// Every significant action in the system must be recorded.
/// </summary>
public class AuditTrailEntry
{
    public Guid Id { get; set; }

    /// <summary>The entity type being audited (e.g., "CashOrder").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The ID of the entity being audited.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Description of the action performed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Severity level of the audit entry.</summary>
    public AuditSeverity Severity { get; set; }

    /// <summary>Additional details or context.</summary>
    public string? Details { get; set; }

    /// <summary>Timestamp of the action.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>The tenant (bank client) context in which the action occurred.</summary>
    public int? BankClientId { get; set; }
}
