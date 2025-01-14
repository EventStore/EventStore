[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Package version number", Mandatory=$true)]
    [string]$Version = "0.0.0.0",
    [Parameter(HelpMessage="OSS/Commercial")]
    [ValidateSet("oss","commercial")]
    [string]$BuildType = "oss",
    [Parameter(HelpMessage="Runtime", Mandatory=$true)]
    [string]$Runtime = "win10-x64"
)

$configuration = "Release"
$targetFramework = "netcoreapp3.1"
$targetRuntime=$Runtime

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$ossSrcDirectory = Join-Path $baseDirectory "oss\src"
$commercialSrcDirectory = Join-Path $baseDirectory "src"
$binDirectory = Join-Path (Join-Path $baseDirectory "oss\bin") $targetFramework

$stagingDirectory = Join-Path $binDirectory "packaged"
$outputDirectory = Join-Path $baseDirectory "packages"

$packageName = ""
if($BuildType -eq "oss"){
    $packageName="EventStore-OSS-Win-v$Version.zip"
} elseif($BuildType -eq "commercial") {
    $packageName="EventStore-Commercial-Win-v$Version.zip"
}

Function Stage-Build() {

    Publish-Project (Join-Path $ossSrcDirectory 'EventStore.ClusterNode')
    Publish-Project (Join-Path $ossSrcDirectory 'EventStore.TestClient')

    if ($BuildType -eq "commercial"){
        Publish-Plugin -ProjectDirectory (Join-Path $commercialSrcDirectory 'EventStore.Auth.Ldaps') -PluginName 'EventStore.Auth.Ldaps'
    }
}

Function Publish-Project
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$ProjectDirectory,
        [Parameter(Mandatory=$false, Position=1)][string]$ErrorMessage = ("Failed publishing {0}" -F $ProjectDirectory)
    )
    Write-Host "Publishing $ProjectDirectory to $stagingDirectory"

    & dotnet publish $ProjectDirectory -c $configuration -r $targetRuntime -f $targetFramework -o $stagingDirectory --no-build /p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw ("Exec: " + $ErrorMessage)
    }
}

Function Publish-Plugin
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$ProjectDirectory,
        [Parameter(Mandatory=$true, Position=0)][string]$PluginName,
        [Parameter(Mandatory=$false, Position=1)][string]$ErrorMessage = ("Failed publishing {0}" -F $ProjectDirectory)
    )
    $pluginDirectory = Join-Path (Join-Path $stagingDirectory "plugins") $PluginName

    Write-Host "Publishing $ProjectDirectory to $pluginDirectory"

    & dotnet publish $ProjectDirectory -c $configuration -r $targetRuntime -f $targetFramework -o $pluginDirectory --no-build /p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw ("Exec: " + $ErrorMessage)
    }
}

Function Package-Build() {
    try {
        if ((Test-Path $outputDirectory) -eq $false) {
            New-Item -Path $outputDirectory -ItemType Directory > $null
        }

        Push-Location $stagingDirectory
        $archiveDestination = Join-Path $outputDirectory $packageName
        Compress-Archive -Path ".\*" -DestinationPath $archiveDestination -CompressionLevel "Optimal"
    } finally {
        Pop-Location
    }
}

Function CleanUp() {
    try {
        Push-Location $baseDirectory
        Remove-Item -Recurse -Force $stagingDirectory -ErrorAction SilentlyContinue > $null
    } finally {
        Pop-Location
    }
}

Stage-Build
Package-Build
CleanUp
