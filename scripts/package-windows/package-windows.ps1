[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Package version number", Mandatory=$true)]
    [string]$Version
)

$packageName = "EventStore-OSS-Win-v$Version.zip"

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$binDirectory = Join-Path $baseDirectory "bin"
$mergedAssembliesPath = Join-Path $binDirectory "merged"
$stagingDirectory = Join-Path $binDirectory "packaged"
$outputDirectory = Join-Path $baseDirectory "packages"
$mergePath = (Join-Path $PSScriptRoot "merge-assemblies.ps1")
$toolsPath = Join-Path $baseDirectory "tools"
$zipPath = Join-Path $toolsPath (Join-Path "zip" "zip.exe")

if ((Test-Path $stagingDirectory) -eq $false) {
    New-Item -Path $stagingDirectory -ItemType Directory > $null
}

if ((Test-Path $outputDirectory) -eq $false) {
    New-Item -Path $outputDirectory -ItemType Directory > $null
}

Function Exec
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][scriptblock]$Command,
        [Parameter(Mandatory=$false, Position=1)][string]$ErrorMessage = ("Failed executing {0}" -F $Command)
    )
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw ("Exec: " + $ErrorMessage)
    }
}

Function Stage-Build() {
    Write-Host "Merging assemblies..."
    Start-Process -NoNewWindow -Wait -FilePath "powershell" -ArgumentList @("-File", "$mergePath")

    Write-Host "Staging merged assemblies..."
    Get-ChildItem -Path $mergedAssembliesPath |
        ForEach-Object { Copy-Item $_.FullName $stagingDirectory }

    Write-Host "Staging supporting files from cluster-node..."

    #Stage cluster node files
    $clusterNodeDirectory = Join-Path $binDirectory "clusternode"
    Copy-Item (Join-Path $clusterNodeDirectory "NLog.config") $stagingDirectory
    Copy-Item -Recurse -Force (Join-Path $clusterNodeDirectory "Prelude") $stagingDirectory
    Copy-Item -Recurse -Force (Join-Path $clusterNodeDirectory "clusternode-web") $stagingDirectory
    Copy-Item -Recurse -Force (Join-Path $clusterNodeDirectory "web-resources") $stagingDirectory
    Copy-Item -Recurse -Force (Join-Path $clusterNodeDirectory "projections") $stagingDirectory
}

Function Package-Build() {
    try {
        Push-Location $stagingDirectory
        $archiveDestination = Join-Path $outputDirectory $packageName
        Start-Process -NoNewWindow -Wait -FilePath $zipPath -ArgumentList @("-9", "-r", "$archiveDestination", "*.*")
    } finally {
        Pop-Location
    }
}

Function CleanUp() {
    try {
        Push-Location $baseDirectory
        Remove-Item -Recurse -Force (Join-Path $binDirectory "merged")
        Remove-Item -Recurse -Force (Join-Path $binDirectory "packaged")
    } finally {
        Pop-Location
    }
}

Stage-Build
Package-Build
CleanUp
