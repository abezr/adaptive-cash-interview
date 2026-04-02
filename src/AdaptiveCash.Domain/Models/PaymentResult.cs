namespace AdaptiveCash.Domain.Models;

public class PaymentResult
{
    public Guid OrderId { get; set; }
    public bool IsSuccess { get; set; }
}
