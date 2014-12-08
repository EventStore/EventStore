# Setup and Configuration
$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$srcDirectory = Join-Path $baseDirectory "src"
$toolsDirectory = Join-Path $baseDirectory "tools"
$documentationOutputPath = Join-Path $PSScriptRoot "documentation.md"
$documentationGenPath = Join-Path $toolsDirectory (Join-Path "documentation-generation" "EventStore.Documentation.exe")

try {
    Push-Location $baseDirectory

    $eventStoreClusterNodePath = Join-Path $baseDirectory "bin\clusternode"

    Start-Process -NoNewWindow -Wait -FilePath $documentationGenPath -ArgumentList @("--event-store-binary-paths=$eventStoreClusterNodePath --output-path=$documentationOutputPath")

} finally {
    Pop-Location
}