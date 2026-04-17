var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

// In-memory notification log (observability hook point)
var notifications = new List<NotificationRecord>();

app.UseHttpsRedirection();

// Request logging middleware — good place to later add structured logging / tracing
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[NotificationService] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} ({elapsed:F1}ms)");
});

app.MapGet("/health", () => Results.Ok(new { service = "notification-service", status = "ok", port = 5003 }));

app.MapGet("/notifications", () => Results.Ok(new { count = notifications.Count, notifications }));

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
});

app.Run();

record NotificationRequest(string OrderId, string CustomerId, string Message);
record NotificationRecord(int Id, string OrderId, string CustomerId, string Message, DateTime SentAt);
