# Test Chat API with SSE response capture
$uri = "http://localhost:5000/api/chat/stream"
$body = @{
    userId = "user123"
    conversationId = "conv-abc"
    message = "What are the best practices for modernization?"
} | ConvertTo-Json

Write-Output "=== Testing Chat API ==="
Write-Output "Endpoint: $uri"
Write-Output "Request: $body"
Write-Output ""
Write-Output "=== RESPONSE (SSE Events) ==="

try {
    $request = [System.Net.HttpWebRequest]::Create($uri)
    $request.Method = "POST"
    $request.ContentType = "application/json"
    $request.Timeout = 30000

    # Write request body
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $stream = $request.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()

    # Get response
    $response = $request.GetResponse()
    $reader = [System.IO.StreamReader]::new($response.GetResponseStream())

    $eventNumber = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        if ($line.StartsWith("event:")) {
            $eventNumber++
            Write-Output ""
            Write-Output "--- Event $eventNumber ---"
            Write-Output $line
        }
        elseif ($line.StartsWith("data:")) {
            $data = $line.Substring(6).Trim()
            Write-Output "Data: $data"
        }
    }

    $reader.Close()
    $response.Close()

    Write-Output ""
    Write-Output "=== SUCCESS ==="
    Write-Output "Chat response received and displayed above"
}
catch {
    Write-Output "ERROR: $_"
}
