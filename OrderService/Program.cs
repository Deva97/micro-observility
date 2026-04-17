var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Order Service API",
        Version = "v1",
        Description = "Handles order creation and delegates to InventoryService for stock reservation."
    });
});

builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
});

var app = builder.Build();

// In-memory order store
var orders = new List<Order>();
var orderCounter = 0;

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Request logging middleware — observability hook point
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[OrderService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "order-service", status = "ok", port = 5001 }))
    .WithName("GetHealth")
    .WithSummary("Health check")
    .WithDescription("Returns service health status.")
    .WithTags("Health");

app.MapGet("/orders", () => Results.Ok(new { count = orders.Count, orders }))
    .WithName("GetOrders")
    .WithSummary("List all orders")
    .WithDescription("Returns all orders in the in-memory store.")
    .WithTags("Orders");

app.MapGet("/orders/{id}", (int id) =>
{
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is null ? Results.NotFound(new { error = $"Order {id} not found" }) : Results.Ok(order);
})
    .WithName("GetOrderById")
    .WithSummary("Get order by ID")
    .WithDescription("Returns a single order by its numeric ID.")
    .WithTags("Orders");

app.MapPost("/orders", async (CreateOrderRequest req, IHttpClientFactory httpFactory) =>
{
    if (string.IsNullOrEmpty(req.CustomerId) || string.IsNullOrEmpty(req.ItemId) || req.Quantity <= 0)
        return Results.BadRequest(new { error = "CustomerId, ItemId, Quantity (>0) required" });

    var orderId = $"ORD-{++orderCounter:D4}";

    Console.WriteLine($"[OrderService] Creating order {orderId} for customer {req.CustomerId}: {req.Quantity}x {req.ItemId}");

    string status;
    string? inventoryError = null;

    try
    {
        var client = httpFactory.CreateClient("inventory");
        var payload = new
        {
            OrderId = orderId,
            CustomerId = req.CustomerId,
            ItemId = req.ItemId,
            Quantity = req.Quantity
        };

        var response = await client.PostAsJsonAsync("/inventory/reserve", payload);

        if (response.IsSuccessStatusCode)
        {
            status = "confirmed";
            Console.WriteLine($"[OrderService] Inventory reserved for order {orderId}");
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync();
            status = "failed";
            inventoryError = $"InventoryService returned {(int)response.StatusCode}: {body}";
            Console.WriteLine($"[OrderService] Inventory reservation failed for {orderId}: {inventoryError}");
        }
    }
    catch (Exception ex)
    {
        status = "failed";
        inventoryError = $"InventoryService unreachable: {ex.Message}";
        Console.WriteLine($"[OrderService] {inventoryError}");
    }

    var order = new Order(
        Id: orderCounter,
        OrderId: orderId,
        CustomerId: req.CustomerId,
        ItemId: req.ItemId,
        Quantity: req.Quantity,
        Status: status,
        CreatedAt: DateTime.UtcNow,
        Error: inventoryError
    );

    orders.Add(order);

    return status == "confirmed"
        ? Results.Created($"/orders/{order.Id}", order)
        : Results.UnprocessableEntity(order);
})
    .WithName("CreateOrder")
    .WithSummary("Create a new order")
    .WithDescription("Creates an order and calls InventoryService to reserve stock. On success InventoryService also triggers NotificationService.")
    .WithTags("Orders");

app.Run();

record CreateOrderRequest(string CustomerId, string ItemId, int Quantity);

record Order(
    int Id,
    string OrderId,
    string CustomerId,
    string ItemId,
    int Quantity,
    string Status,
    DateTime CreatedAt,
    string? Error
);
