# Micro-Observability

.NET 10 microservices boilerplate for learning observability patterns.

## Services

```
POST /orders (5001) → POST /inventory/reserve (5002) → POST /notify (5003)
```

| Service             | Port | Swagger UI                    |
|---------------------|------|-------------------------------|
| OrderService        | 5001 | http://localhost:5001/swagger |
| InventoryService    | 5002 | http://localhost:5002/swagger |
| NotificationService | 5003 | http://localhost:5003/swagger |

## Run

```bash
./run-all.sh
```

## Quick Test

```bash
# Triggers full chain: order → reserve stock → notify
curl -X POST http://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C001","itemId":"ITEM-001","quantity":5}'

curl http://localhost:5003/notifications  # verify end-to-end
```

**Seeded inventory:** `ITEM-001` (100), `ITEM-002` (50), `ITEM-003` (20)

## Observability Hook Points

Each service has request logging middleware — ready to extend with OpenTelemetry, Serilog, or Prometheus.
