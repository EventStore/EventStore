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

function Get-AllProjections {
    param (
        $Mode
    )

    # Projections API doesn't have pagination, so we just need to send the one request
    $listResponse = Invoke-RestMethod -Uri "${EventStoreAddress}/projections/${Mode}" -Headers $Headers
    $details = foreach ($projection in $listResponse.Projections) {
        @{
            Name = $projection.EffectiveName
            DisableUri = $projection.DisableCommandUrl
            EnableUri = $projection.EnableCommandUrl
            StatusUri = $projection.StatusUrl
            ConfigUri = "${EventStoreAddress}/projection/$($projection.EffectiveName)/config"
            Status = $projection.Status
        }
    }
    $details
}

function Get-ProjectionConfig {
    param (
        $ProjectionDetails
    )

    Write-Host "Getting projection config for $($ProjectionDetails.Name)"
    Invoke-RestMethod -Uri $ProjectionDetails.ConfigUri -Headers $Headers
}

function Watch-ProjectionStatus {
    param (
        $ProjectionDetails,
        $ExpectedStatus
    )
    Write-Host "Waiting for projection $($projection.Name) to enter status $ExpectedStatus"
    $complete = $false
    while ($false -eq $complete) {
        $status = Invoke-RestMethod -Uri $ProjectionDetails.StatusUri -Headers $Headers
        if ($ExpectedStatus -eq $status.Status) {
            $complete = $true
        } else {
            Start-Sleep -Milliseconds 500
        }
    }
}

function Disable-Projection {
    param (
        $ProjectionDetails
    )

    Write-Host "Disabling projection $($ProjectionDetails.Name)"
    Invoke-WebRequest -Uri $ProjectionDetails.DisableUri -Method "POST" -Headers $Headers | Out-Null

    Watch-ProjectionStatus $ProjectionDetails @("Stopped", "Faulted")

    Write-Host "Projection $($ProjectionDetails.Name) has been disabled"
}

function Enable-Projection {
    param (
        $ProjectionDetails
    )

    Write-Host "Enabling projection $($ProjectionDetails.Name)"
    Invoke-WebRequest -Uri $ProjectionDetails.EnableUri -Method "POST" -Headers $Headers | Out-Null

    Watch-ProjectionStatus $ProjectionDetails @("Running")

    Write-Host "Projection $($ProjectionDetails.Name) has been enabled"
}

function Update-ProjectionConfig {
    param (
        $ProjectionDetails,
        $ProjectionConfig
    )
    Write-Host "Removing projection execution timeout from projection $($ProjectionDetails.Name)"

    # Remove the ProjectionExecutionTimeout
    $ProjectionConfig.PSObject.Properties.Remove('projectionExecutionTimeout')

    # Post the config back to the projection
    $body = ConvertTo-Json $ProjectionConfig
    Invoke-WebRequest -Uri $ProjectionDetails.ConfigUri -Header $Headers -Method "PUT" -Body $body | Out-Null

    Write-Host "Configuration for projection $($ProjectionDetails.Name) has been updated"
}

$AllProjections = Get-AllProjections "all-non-transient"
Write-Host "Found $($AllProjections.Count) projections"

foreach($projection in $AllProjections) {
    
    Write-Host "`nChecking projection $($projection.Name)"
    $projectionConfig = Get-ProjectionConfig $projection

    if ($null -ne $projectionConfig.ProjectionExecutionTimeout) {
        Write-Host "Fixing projection $($projection.Name)"
        if ($projection.Status -ne "Stopped" -and $projection.Status -ne "Faulted") {
            Disable-Projection $projection
        }

        Update-ProjectionConfig $projection $projectionConfig

        # Only enable the projection if it was running in the first place
        if ($projection.Status -eq "Running") {
            Enable-Projection $projection
        }
    } else {
        Write-Host "Projection $($projection.Name) has no projection execution timeout. Skipping."
    }
}
