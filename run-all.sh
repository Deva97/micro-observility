#!/bin/bash
# Start all services in background, kill all on Ctrl+C

trap "echo 'Stopping all services...'; kill 0" SIGINT SIGTERM

echo "Starting NotificationService on http://localhost:5003..."
cd NotificationService && dotnet run --launch-profile http &

sleep 2

echo "Starting InventoryService on http://localhost:5002..."
cd ../InventoryService && dotnet run --launch-profile http &

sleep 2

echo "Starting OrderService on http://localhost:5001..."
cd ../OrderService && dotnet run --launch-profile http &

echo ""
echo "All services running. Press Ctrl+C to stop."
wait
