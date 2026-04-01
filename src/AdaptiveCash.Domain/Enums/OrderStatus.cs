namespace AdaptiveCash.Domain.Enums;

/// <summary>
/// Represents the processing status of a cash order within the system.
/// See C4 Component diagram for the full state machine transitions.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order received but not yet validated.</summary>
    Received = 0,

    /// <summary>Order passed all validation rules.</summary>
    Validated = 1,

    /// <summary>Order is being processed by the settlement engine.</summary>
    Processing = 2,

    /// <summary>Order confirmed by the external banking system.</summary>
    Confirmed = 3,

    /// <summary>Order fully completed and settled.</summary>
    Completed = 4,

    /// <summary>Order rejected during validation or processing.</summary>
    Rejected = 5,

    /// <summary>Order failed during external system confirmation.</summary>
    Failed = 6
}
