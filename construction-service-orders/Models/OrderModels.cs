namespace ConstructionServiceOrders.Models;

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string Status { get; set; } = "created";
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
}

public class CreateOrderRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
}

public class UpdateOrderRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public string? Status { get; set; }
}