# Setup and Configuration
$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$srcDirectory = Join-Path $baseDirectory "src"
$toolsDirectory = Join-Path $baseDirectory "tools"
$documentationOutputPath = Join-Path $toolsDirectory (Join-Path "documentation-generation" "documentation.md")
$documentationGenPath = Join-Path $toolsDirectory (Join-Path "documentation-generation" "EventStore.Documentation.exe")

try {
    Push-Location $baseDirectory

    $eventStoreSingleNodePath = Join-Path $baseDirectory "bin\singlenode"
    $eventStoreClusterNodePath = Join-Path $baseDirectory "bin\clusternode"

    Start-Process -NoNewWindow -Wait -FilePath $documentationGenPath -ArgumentList @("-b $eventStoreSingleNodePath,$eventStoreClusterNodePath -o $documentationOutputPath")

} finally {
    Pop-Location
}