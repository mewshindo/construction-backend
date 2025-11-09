using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Construction Orders Service",
        Version = "v1",
        Description = "Handles all orders for the construction system."
    });
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders Service v1");
});


var fakeOrdersDb = new Dictionary<int, dynamic>();
var currentId = 1;

// ===== STATUS & HEALTH =====

app.MapGet("/orders/status", () =>
{
    return Results.Json(new { status = "Orders service is running" });
});

app.MapGet("/orders/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        service = "Orders Service",
        timestamp = DateTime.UtcNow.ToString("o")
    });
});

// ===== GET ORDER BY ID =====

app.MapGet("/orders/{orderId:int}", (int orderId) =>
{
    if (!fakeOrdersDb.TryGetValue(orderId, out var order))
        return Results.NotFound(new { error = "Order not found" });

    return Results.Ok(order);
});

// ===== GET ALL ORDERS =====

app.MapGet("/orders", ([FromQuery] int? userId) =>
{
    IEnumerable<dynamic> orders = fakeOrdersDb.Values;

    if (userId.HasValue)
    {
        orders = orders.Where(o => (int)o.userId == userId.Value);
    }

    return Results.Ok(orders);
});

// ===== CREATE ORDER =====

app.MapPost("/orders", (dynamic orderData) =>
{
    int orderId = currentId++;

    var newOrder = new
    {
        id = orderId,
        orderData.userId,
        orderData.items,
        orderData.total,
        orderData.status
    };

    fakeOrdersDb[orderId] = newOrder;

    return Results.Created($"/orders/{orderId}", newOrder);
});

// ===== UPDATE ORDER =====

app.MapPut("/orders/{orderId:int}", (int orderId, dynamic orderData) =>
{
    if (!fakeOrdersDb.ContainsKey(orderId))
        return Results.NotFound(new { error = "Order not found" });

    var updatedOrder = new
    {
        id = orderId,
        orderData.userId,
        orderData.items,
        orderData.total,
        orderData.status
    };

    fakeOrdersDb[orderId] = updatedOrder;

    return Results.Ok(updatedOrder);
});

// ===== DELETE ORDER =====

app.MapDelete("/orders/{orderId:int}", (int orderId) =>
{
    if (!fakeOrdersDb.TryGetValue(orderId, out var orderToDelete))
        return Results.NotFound(new { error = "Order not found" });

    fakeOrdersDb.Remove(orderId);

    return Results.Ok(new
    {
        message = "Order deleted",
        deletedOrder = orderToDelete
    });
});

app.Run();
