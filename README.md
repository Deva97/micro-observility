# Micro-Observability

Boilerplate microservices project for learning observability patterns in .NET 10.

## Architecture

Three services communicate over HTTP REST. Each call fans out through the chain:

```
POST /orders  (OrderService :5001)
      │
      └─► POST /inventory/reserve  (InventoryService :5002)
                │
                └─► POST /notify  (NotificationService :5003)
```

| Service             | Port | Responsibility                              |
|---------------------|------|---------------------------------------------|
| OrderService        | 5001 | Creates orders, delegates stock reservation |
| InventoryService    | 5002 | Manages stock levels, triggers notification |
| NotificationService | 5003 | Receives and logs notification events       |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

**All services at once:**
```bash
./run-all.sh
```

**Individual service:**
```bash
cd OrderService && dotnet run --launch-profile http
cd InventoryService && dotnet run --launch-profile http
cd NotificationService && dotnet run --launch-profile http
```

## Swagger UI

Each service exposes a Swagger UI for interactive API exploration:

| Service             | Swagger UI                          | OpenAPI JSON                               |
|---------------------|-------------------------------------|--------------------------------------------|
| OrderService        | http://localhost:5001/swagger       | http://localhost:5001/swagger/v1/swagger.json |
| InventoryService    | http://localhost:5002/swagger       | http://localhost:5002/swagger/v1/swagger.json |
| NotificationService | http://localhost:5003/swagger       | http://localhost:5003/swagger/v1/swagger.json |

## API Reference

### OrderService — `http://localhost:5001`

| Method | Route         | Description                        |
|--------|---------------|------------------------------------|
| GET    | /health       | Health check                       |
| GET    | /orders       | List all orders                    |
| GET    | /orders/{id}  | Get order by ID                    |
| POST   | /orders       | Create order (triggers full chain) |

**Create order request:**
```json
{
  "customerId": "C001",
  "itemId": "ITEM-001",
  "quantity": 5
}
```

### InventoryService — `http://localhost:5002`

| Method | Route                    | Description                  |
|--------|--------------------------|------------------------------|
| GET    | /health                  | Health check                 |
| GET    | /inventory               | List all stock levels        |
| GET    | /inventory/{itemId}      | Get stock for specific item  |
| POST   | /inventory/reserve       | Reserve stock for an order   |

**Seeded items:** `ITEM-001` (100), `ITEM-002` (50), `ITEM-003` (20)

### NotificationService — `http://localhost:5003`

| Method | Route           | Description                   |
|--------|-----------------|-------------------------------|
| GET    | /health         | Health check                  |
| GET    | /notifications  | List all received events      |
| POST   | /notify         | Receive a notification event  |

## Test the Full Chain

```bash
# Create an order — triggers inventory reservation + notification
curl -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C001","itemId":"ITEM-001","quantity":5}'

# Verify order was created
curl http://localhost:5001/orders

# Verify stock was decremented
curl http://localhost:5002/inventory/ITEM-001

# Verify notification was received
curl http://localhost:5003/notifications
```

## Observability Hook Points

Each service has a request logging middleware (`Program.cs`) that prints method, path, status code, and latency to stdout. This is intentionally minimal — the natural next steps are:

- **Structured logging** — replace `Console.WriteLine` with Serilog / Microsoft.Extensions.Logging
- **Distributed tracing** — add OpenTelemetry with trace context propagation across service calls
- **Metrics** — instrument with OpenTelemetry metrics or Prometheus exporters
- **Health checks** — extend `/health` with `Microsoft.Extensions.Diagnostics.HealthChecks`

## Project Structure

```
micro-observility/
├── OrderService/           # Port 5001
├── InventoryService/       # Port 5002
├── NotificationService/    # Port 5003
├── MicroObservability.sln
└── run-all.sh
```
