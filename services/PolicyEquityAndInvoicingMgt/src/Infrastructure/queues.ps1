# RabbitMQ Topology Guidance for Billing Service
# NServiceBus installers create required queues/bindings automatically in development.

Write-Host "RabbitMQ transport is enabled for Billing." -ForegroundColor Green
Write-Host "No manual queue bootstrap is required when installers are enabled." -ForegroundColor Green
Write-Host ""
Write-Host "If you need infrastructure manually:" -ForegroundColor Yellow
Write-Host "  1. Ensure RabbitMQ is running (docker compose up -d rabbitmq)" -ForegroundColor White
Write-Host "  2. Start Billing Endpoint.In with installers enabled" -ForegroundColor White
Write-Host "  3. Verify queues in RabbitMQ management UI (http://localhost:15672)" -ForegroundColor White
