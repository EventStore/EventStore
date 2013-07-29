function Upgrade-ESProjections {
<#
    .SYNOPSIS
    Upgrades projections for EventStore v2.

    .DESCRIPTION
    Start the EventStore v2 on existing data.
    Run this Cmdlet with the store http endpoint and user credentials.
    Restart the EventStore v2. Projections should be updated.


    .PARAMETER Store
    The http address of the EventStore



#>
    param(
        [string]$Store = "http://localhost:2113",
        [pscredential]$Credential = (Get-Credential admin)
    )

    end {
        Write-Host Starting upgrade

        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $session.Credentials = $credential
        
        function Get-AllStreamEventsRef([string]$Stream) {
            $url = "$store/streams/$stream/0/forward/20"

            do {
                $r = Invoke-RestMethod -Uri ($url + "?format=json") -WebSession $session
                if ($r.entries.Count -gt 0)
                {
                    $r.entries `
                    | Add-Member -MemberType ScriptProperty -Name version -Value { [int]($this.title.Split('@')[0]) } -PassThru `
                    | Sort-Object -Property version
                }
                $url = $r.links | Where-Object relation -eq previous | ForEach-Object uri
            } while( $r.entries.Count -gt 0)

        }

        function Get-EventData {
            param(
                [Parameter(ValueFromPipeline)]$Event,
                [switch]$DataOnly = $false
                )
            process {
                $uri = $event.links `
                       | Where-Object relation -eq edit `                       | ForEach-Object uri
                if ($DataOnly) {
                    $uri += '?format=json'
                }

                Invoke-RestMethod -Uri $uri -WebSession $session
            }
        }

        function Append-Event {
            param(
                [string]$Stream,
                [string]$EventType, 
                $Data,
                $Metadata
                )

            process
            {
                $json = ConvertTo-Json @(, @{
                    eventId = [Guid]::NewGuid()
                    eventType = $EventType
                    data = $Data
                    metadata = $Metadata
                }) -Depth 10

                Invoke-WebRequest -Uri "$store/streams/$stream" -Method Post  `
                                  -ContentType "application/json" -Body $json `                                  -Headers @{'ES-ExpectedVersion' = "-2"}     `
                                  -WebSession $session
            }
        }


        $projectionsall = "%24projections-%24all"
        $projectionRefs = Get-AllStreamEventsRef $projectionsall
        
        $oldProjections = $projectionRefs `
                          | Where-Object summary -eq ProjectionCreated `
                          | Get-EventData -DataOnly
        $newProjections = $projectionRefs `                          | Where-Object summary -eq '$ProjectionCreated' `
                          | Get-EventData -DataOnly

        # take projections names that have no match with $ProjectionCreated event
        $toConvert = diff $oldProjections $newProjections `                     | Where-Object SideIndicator -eq '<=' `                     | ForEach-Object InputObject

        Write-Host $toConvert.Count projections to upgrade in '$projections-$all'

        # append a $ProjectionCreated event in $projections-$all for projections
        # that have not been converted yet
        $toConvert | ForEach-Object { 
            Write-Host converting ProjectionCreated for $_
            Append-Event $projectionsall '$ProjectionCreated' $_ | Out-Null
        }

        # append a copy of the last ProjectionUpdated event as a $ProjectionUpdated event
        # when projection has not been converted yet
        $oldProjections | ForEach-Object {

            #convert projections ProjectionUpdated event to $ProjectionUpdated
            $projectionName = "`$projections-$_"

            $events = Get-AllStreamEventsRef $projectionName

            $newEvents = $events | Where-Object summary -eq '$ProjectionUpdated'

            if (!$newEvents) {

                $events `                | Where-Object summary -eq "ProjectionUpdated" `                | Select-Object -Last 1 `
                | ForEach-Object {
                    Write-Host converting ProjectionUpdated for $projectionName
                    $data = $_ | Get-EventData

                    Append-Event $projectionName '$ProjectionUpdated' $data.content.data $data.content.metadata | Out-Null
                }
            }
            else {
                Write-Host Skipping $projectionName. Already converted -ForegroundColor Gray
            }


            #convert checkpoints ProjectionCheckpoint to $ProjectionCheckpoint

            $checkpointName = "$projectionName-checkpoint"

            $checkpointEvents = Get-AllStreamEventsRef $checkpointName

            $newcheckPointEvents = $checkpointEvents | Where-Object summary -eq '$ProjectionCheckpoint'

            if (!$newcheckPointEvents) {

                $checkpointEvents `                | Where-Object summary -eq "ProjectionCheckpoint" `                | Select-Object -Last 1 `
                | ForEach-Object {
                    Write-Host converting ProjectionCheckpoint for $checkpointName
                    $data = $_ | Get-EventData
                    Append-Event $checkpointName '$ProjectionCheckpoint' $data.content.data $data.content.metadata | Out-Null
                }
            }
            else {
                Write-Host Skipping $checkpointName. Already converted -ForegroundColor Gray
            }


        }

        Write-Host Upgrade completed.
        Write-Host Restart the EventStore to make change effective.
    }
}
 