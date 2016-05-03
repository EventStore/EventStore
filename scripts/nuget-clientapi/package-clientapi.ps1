[CmdletBinding()]
Param(
    [Parameter(HelpMessage="NuGet package version number", Mandatory=$true)]
    [string]$Version
)

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$binDirectory = Join-Path $baseDirectory "bin"
$stagingDirectory = Join-Path $binDirectory "nuget"
$outputDirectory = Join-Path $baseDirectory "packages"
$toolsPath = Join-Path $baseDirectory "tools"
$nugetPath = Join-Path $toolsPath (Join-Path "nuget" "nuget.exe")
$nuspecDirectory = Join-Path $baseDirectory (Join-Path "scripts" "nuget-clientapi")
$svnClientPath = Join-Path $toolsPath (Join-Path "svn" "svn.exe")

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

Function Run-NugetPack() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$NuspecPath
    )
    Start-Process -NoNewWindow -Wait -FilePath $NugetPath -ArgumentList @("pack", "-symbols", "-version $Version", "$NuspecPath")
    if ($LASTEXITCODE -eq 0) {
        Move-Item -Path *.nupkg -Destination $outputDirectory
    }
}

Function Get-SourceDependencies() {
    try {
        Push-Location $stagingDirectory
        if ((Test-Path "protobuf-net-read-only") -eq $false) {
            $svnCommand = "$svnClientPath checkout --quiet -r 594 https://protobuf-net.googlecode.com/svn/trunk/ protobuf-net-read-only"
            Exec([ScriptBlock]::Create($svnCommand))
        }

        if ((Test-Path "Newtonsoft.Json") -eq $false) {
            Exec { git clone https://github.com/JamesNK/Newtonsoft.Json Newtonsoft.Json 2> $null }
            try {
                Push-Location Newtonsoft.Json
                Exec { git checkout 6.0.1 2> $null }
            } finally {
                Pop-Location
            }
        }
    } finally {
        Pop-Location
    }
}

Get-SourceDependencies
Run-NugetPack -NuspecPath (Join-Path $nuspecDirectory "EventStore.Client.nuspec")
Run-NugetPack -NuspecPath (Join-Path $nuspecDirectory "EventStore.Client.Embedded.nuspec")
