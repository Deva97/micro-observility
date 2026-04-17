var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
});

var app = builder.Build();

// In-memory order store
var orders = new List<Order>();
var orderCounter = 0;

app.UseHttpsRedirection();

// Request logging middleware — observability hook point
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[OrderService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "order-service", status = "ok", port = 5001 }));

app.MapGet("/orders", () => Results.Ok(new { count = orders.Count, orders }));

app.MapGet("/orders/{id}", (int id) =>
{
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is null ? Results.NotFound(new { error = $"Order {id} not found" }) : Results.Ok(order);
});

// POST /orders — create order, reserve inventory
app.MapPost("/orders", async (CreateOrderRequest req, IHttpClientFactory httpFactory) =>
{
    if (string.IsNullOrEmpty(req.CustomerId) || string.IsNullOrEmpty(req.ItemId) || req.Quantity <= 0)
        return Results.BadRequest(new { error = "CustomerId, ItemId, Quantity (>0) required" });

    var orderId = $"ORD-{++orderCounter:D4}";

    Console.WriteLine($"[OrderService] Creating order {orderId} for customer {req.CustomerId}: {req.Quantity}x {req.ItemId}");

    // Call InventoryService to reserve stock
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
});

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
