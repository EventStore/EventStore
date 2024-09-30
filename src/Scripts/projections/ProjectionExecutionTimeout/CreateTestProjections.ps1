param (
    $EventStoreAddress,
    $AdminUsername,
    [SecureString] $AdminPassword
)

# Set up basic auth credentials
$password = ConvertFrom-SecureString $AdminPassword -AsPlainText
$pair = "${AdminUsername}:${password}"
$encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
$basicAuthValue = "Basic $encodedCreds"

$Headers = @{
    Authorization = $basicAuthValue;
    Accept = 'application/json';
    "Content-Type" = 'application/json';
}


For ($i=0; $i -le 50; $i++) {
    $projectionName = "test-projection-${i}"
    Write-Host "Creating projection $projectionName"

    $body = "{ fromAll().when() }"
    Invoke-WebRequest "${EventStoreAddress}/projections/continuous?name=${projectionName}" -Method "POST" -Body $body -Headers $Headers
}