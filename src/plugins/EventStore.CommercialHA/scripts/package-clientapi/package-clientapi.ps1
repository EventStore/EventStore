[CmdletBinding()]
Param(
    [Parameter(HelpMessage="NuGet package version number", Mandatory=$true)]
    [string]$Version
)

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$configuration = "Release"
$outputDirectory = Join-Path $baseDirectory "packages"

if ((Test-Path $outputDirectory) -eq $false) {
    New-Item -Path $outputDirectory -ItemType Directory > $null
}

Function RunDotnetPack() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$ProjectPath
    )
    Start-Process -NoNewWindow -Wait -FilePath "dotnet" -ArgumentList @("pack", "-c", $configuration, "-o", $outputDirectory, "/p:Version=$Version", "$ProjectPath")
}

RunDotnetPack -ProjectPath (Join-Path $baseDirectory "oss\src\EventStore.ClientAPI\EventStore.ClientAPI.csproj")
RunDotnetPack -ProjectPath (Join-Path $baseDirectory "oss\src\EventStore.ClientAPI.Embedded\EventStore.ClientAPI.Embedded.csproj")
RunDotnetPack -ProjectPath (Join-Path $baseDirectory "oss\src\EventStore.Client\EventStore.Client.csproj")
RunDotnetPack -ProjectPath (Join-Path $baseDirectory "oss\src\EventStore.Client.Operations\EventStore.Client.Operations.csproj")
