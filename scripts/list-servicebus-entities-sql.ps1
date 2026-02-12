param(
    [string]$SqlPassword = $ENV:SQL_SERVER_SA_PASSWORD
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Service Bus Emulator Status" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Queues
Write-Host "[QUEUES PROVISIONED IN EMULATOR]" -ForegroundColor Green
$queues = docker exec sql-server sqlcmd -h -1 -W -S localhost -U sa -P $SqlPassword `
  -Q "SELECT QueueName FROM [servicebus].dbo.QueueDescription ORDER BY QueueName" | 
  Where-Object {$_ -match '\S'} | ForEach-Object {$_.Trim()}

if ($queues.Count -gt 0) {
    $queues | ForEach-Object { Write-Host "  ✓ $_" -ForegroundColor White }
    Write-Host "  Total: $($queues.Count) queues" -ForegroundColor Cyan
} else {
    Write-Host "  No queues found" -ForegroundColor Yellow
}
Write-Host ""

# Topics
Write-Host "[TOPICS PROVISIONED IN EMULATOR]" -ForegroundColor Green
$topics = docker exec sql-server sqlcmd -h -1 -W -S localhost -U sa -P $SqlPassword `
  -Q "SELECT TopicName FROM [servicebus].dbo.TopicDescription ORDER BY TopicName" | 
  Where-Object {$_ -match '\S'} | ForEach-Object {$_.Trim()}

if ($topics.Count -gt 0) {
    $topics | ForEach-Object { Write-Host "  ✓ $_" -ForegroundColor White }
    Write-Host "  Total: $($topics.Count) topics" -ForegroundColor Cyan
} else {
    Write-Host "  No topics found" -ForegroundColor Yellow
}
Write-Host ""

# Subscriptions
Write-Host "[SUBSCRIPTIONS PROVISIONED IN EMULATOR]" -ForegroundColor Green
$subs = docker exec sql-server sqlcmd -h -1 -W -S localhost -U sa -P $SqlPassword `
  -Q "SELECT TopicName + ' -> ' + SubscriptionName AS Subscription FROM [servicebus].dbo.SubscriptionDescription ORDER BY TopicName, SubscriptionName" | 
  Where-Object {$_ -match '\S'} | ForEach-Object {$_.Trim()}

if ($subs.Count -gt 0) {
    $subs | ForEach-Object { Write-Host "  ✓ $_" -ForegroundColor White }
    Write-Host "  Total: $($subs.Count) subscriptions" -ForegroundColor Cyan
} else {
    Write-Host "  No subscriptions found" -ForegroundColor Yellow
}
Write-Host ""

# Compare with config.json
Write-Host "[DEFINED IN CONFIG.JSON]" -ForegroundColor Cyan
$config = Get-Content "..\config.json" -Raw | ConvertFrom-Json
$ns = $config.UserConfig.Namespaces[0]
Write-Host "  Queues: $($ns.Queues.Count)" -ForegroundColor White
Write-Host "  Topics: $($ns.Topics.Count)" -ForegroundColor White
$totalSubs = ($ns.Topics | ForEach-Object {$_.Subscriptions.Count} | Measure-Object -Sum).Sum
Write-Host "  Subscriptions: $totalSubs" -ForegroundColor White
Write-Host ""

if ($queues.Count -eq $ns.Queues.Count -and $topics.Count -eq $ns.Topics.Count) {
    Write-Host "✓ All config.json entities provisioned" -ForegroundColor Green
} else {
    Write-Host "⚠ Mismatch - emulator may need restart" -ForegroundColor Yellow
    Write-Host "  Restart with: docker-compose down servicebus-emulator && docker-compose up -d servicebus-emulator" -ForegroundColor Yellow
}
Write-Host ""