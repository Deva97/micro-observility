var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("notification", client =>
{
    client.BaseAddress = new Uri("http://localhost:5003");
});

var app = builder.Build();

// Seed in-memory inventory
var inventory = new Dictionary<string, int>
{
    ["ITEM-001"] = 100,
    ["ITEM-002"] = 50,
    ["ITEM-003"] = 20,
};

app.UseHttpsRedirection();

// Request logging middleware — observability hook point
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[InventoryService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "inventory-service", status = "ok", port = 5002 }));

app.MapGet("/inventory", () => Results.Ok(inventory));

app.MapGet("/inventory/{itemId}", (string itemId) =>
{
    if (!inventory.TryGetValue(itemId, out var qty))
        return Results.NotFound(new { error = $"Item {itemId} not found" });

    return Results.Ok(new { itemId, quantity = qty });
});

// POST /inventory/reserve — reserve stock and notify
app.MapPost("/inventory/reserve", async (ReserveRequest req, IHttpClientFactory httpFactory) =>
{
    if (string.IsNullOrEmpty(req.ItemId) || req.Quantity <= 0 || string.IsNullOrEmpty(req.OrderId))
        return Results.BadRequest(new { error = "ItemId, Quantity (>0), OrderId required" });

    if (!inventory.TryGetValue(req.ItemId, out var current))
        return Results.NotFound(new { error = $"Item {req.ItemId} not found" });

    if (current < req.Quantity)
        return Results.UnprocessableEntity(new { error = $"Insufficient stock. Available: {current}, Requested: {req.Quantity}" });

    inventory[req.ItemId] = current - req.Quantity;

    Console.WriteLine($"[InventoryService] Reserved {req.Quantity}x {req.ItemId} for order {req.OrderId}. Remaining: {inventory[req.ItemId]}");

    // Call NotificationService
    try
    {
        var client = httpFactory.CreateClient("notification");
        var payload = new
        {
            OrderId = req.OrderId,
            CustomerId = req.CustomerId,
            Message = $"Reserved {req.Quantity}x {req.ItemId}. Stock remaining: {inventory[req.ItemId]}"
        };
        var response = await client.PostAsJsonAsync("/notify", payload);
        Console.WriteLine($"[InventoryService] NotificationService responded: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        // Non-fatal — reservation still succeeded
        Console.WriteLine($"[InventoryService] Failed to notify: {ex.Message}");
    }

    return Results.Ok(new
    {
        success = true,
        itemId = req.ItemId,
        reserved = req.Quantity,
        remaining = inventory[req.ItemId]
    });
});

app.Run();

record ReserveRequest(string OrderId, string CustomerId, string ItemId, int Quantity);
