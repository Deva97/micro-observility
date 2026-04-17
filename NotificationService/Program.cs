var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Notification Service API",
        Version = "v1",
        Description = "Receives and stores notification events from other services. Acts as the tail of the order processing chain."
    });
});

var app = builder.Build();

// In-memory notification log
var notifications = new List<NotificationRecord>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Request logging middleware — observability hook point
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[NotificationService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "notification-service", status = "ok", port = 5003 }))
    .WithName("GetHealth")
    .WithSummary("Health check")
    .WithDescription("Returns service health status.")
    .WithTags("Health");

app.MapGet("/notifications", () => Results.Ok(new { count = notifications.Count, notifications }))
    .WithName("GetNotifications")
    .WithSummary("List all notifications")
    .WithDescription("Returns all notification events received so far.")
    .WithTags("Notifications");

app.MapPost("/notify", (NotificationRequest req) =>
{
    if (string.IsNullOrEmpty(req.OrderId) || string.IsNullOrEmpty(req.CustomerId) || string.IsNullOrEmpty(req.Message))
        return Results.BadRequest(new { error = "OrderId, CustomerId, Message required" });

    var record = new NotificationRecord(
        Id: notifications.Count + 1,
        OrderId: req.OrderId,
        CustomerId: req.CustomerId,
        Message: req.Message,
        SentAt: DateTime.UtcNow
    );

    notifications.Add(record);
    Console.WriteLine($"[NotificationService] Notification sent for order {req.OrderId}: \"{req.Message}\"");

    return Results.Created($"/notifications/{record.Id}", record);
})
    .WithName("SendNotification")
    .WithSummary("Send a notification")
    .WithDescription("Receives a notification event and stores it. Called by InventoryService after stock reservation.")
    .WithTags("Notifications");

app.Run();

record NotificationRequest(string OrderId, string CustomerId, string Message);
record NotificationRecord(int Id, string OrderId, string CustomerId, string Message, DateTime SentAt);
