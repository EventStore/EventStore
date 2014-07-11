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
$nuspecPath = Join-Path $baseDirectory (Join-Path "scripts" (Join-Path "nuget-clientapi" "EventStore.Client.nuspec"))

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
    Start-Process -NoNewWindow -Wait -FilePath $nugetPath -ArgumentList @("pack", "-symbols", "-version $Version", "$nuspecPath")
    if ($LASTEXITCODE -eq 0) {
        Move-Item -Path *.nupkg -Destination $outputDirectory
    }
}

Function Get-SourceDependencies() {
    try {
        Push-Location $stagingDirectory
        if ((Test-Path "protobuf-net-read-only") -eq $false) {
            Exec { svn checkout --quiet -r 594 http://protobuf-net.googlecode.com/svn/trunk/ protobuf-net-read-only }
        }

        if ((Test-Path "Newtonsoft.Json") -eq $false) {
            Exec { git clone https://github.com/JamesNK/Newtonsoft.Json Newtonsoft.Json 2> $null }
            try {
                Push-Location Newtonsoft.Json
                Exec { git checkout 4.5.7 2> $null }
            } finally {
                Pop-Location
            }
        }
    } finally {
        Pop-Location
    }
}

Get-SourceDependencies
Run-NugetPack