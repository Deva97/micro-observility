var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Inventory Service API",
        Version = "v1",
        Description = "Manages stock levels and reserves inventory for orders. Calls NotificationService on successful reservation."
    });
});

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

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Service v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Request logging middleware — observability hook point
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[InventoryService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "inventory-service", status = "ok", port = 5002 }))
    .WithName("GetHealth")
    .WithSummary("Health check")
    .WithDescription("Returns service health status.")
    .WithTags("Health");

app.MapGet("/inventory", () => Results.Ok(inventory))
    .WithName("GetInventory")
    .WithSummary("List all inventory")
    .WithDescription("Returns current stock levels for all items.")
    .WithTags("Inventory");

app.MapGet("/inventory/{itemId}", (string itemId) =>
{
    if (!inventory.TryGetValue(itemId, out var qty))
        return Results.NotFound(new { error = $"Item {itemId} not found" });

    return Results.Ok(new { itemId, quantity = qty });
})
    .WithName("GetInventoryItem")
    .WithSummary("Get stock for a specific item")
    .WithDescription("Returns current quantity for a given item ID.")
    .WithTags("Inventory");

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
        Console.WriteLine($"[InventoryService] Failed to notify: {ex.Message}");
    }

    return Results.Ok(new
    {
        success = true,
        itemId = req.ItemId,
        reserved = req.Quantity,
        remaining = inventory[req.ItemId]
    });
})
    .WithName("ReserveInventory")
    .WithSummary("Reserve stock for an order")
    .WithDescription("Deducts requested quantity from stock and triggers a notification via NotificationService.")
    .WithTags("Inventory");

app.Run();

record ReserveRequest(string OrderId, string CustomerId, string ItemId, int Quantity);
