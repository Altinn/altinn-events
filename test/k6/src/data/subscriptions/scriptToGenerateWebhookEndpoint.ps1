ps
$params = @{
 Uri = "https://webhook.site/token"
 Method = "Post"
};
$webhookToken =((Invoke-Webrequest @params).Content  | ConvertFrom-Json).uuid;
$webhookEndpoint= "https://webhook.site/" + $webhookToken + "/";
Write-Host "Webhook Token: $webhookToken"; 
Write-Host "Webhook Endpoint: $webhookEndpoint";