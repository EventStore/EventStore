[CmdletBinding()]
Param(
    [Parameter(HelpMessage="NuGet package version number", Mandatory=$true)]
    [string]$Version
)

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$binDirectory = Join-Path $baseDirectory "bin"
$stagingDirectory = Join-Path $binDirectory "nuget"
$netcoreNupkgDirectory = Join-Path $binDirectory "dotnetcorenupkgs"
$outputDirectory = Join-Path $baseDirectory "packages"
$toolsPath = Join-Path $baseDirectory "tools"
$nugetPath = Join-Path $toolsPath (Join-Path "nuget" "nuget.exe")
$dotnetcliToolsPath = Join-Path $toolsPath "dotnetcli"
$nuspecDirectory = Join-Path $baseDirectory (Join-Path "scripts" "nuget-clientapi")
$svnClientPath = Join-Path $toolsPath (Join-Path "svn" "svn.exe")

if ((Test-Path $stagingDirectory) -eq $false) {
    New-Item -Path $stagingDirectory -ItemType Directory > $null
}

if ((Test-Path $outputDirectory) -eq $false) {
    New-Item -Path $outputDirectory -ItemType Directory > $null
}

if ((Test-Path $netcoreNupkgDirectory) -eq $false) {
    New-Item -Path $netcoreNupkgDirectory -ItemType Directory > $null
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

Function Run-DotnetPack-NoBuild() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$ProjPath
    )
    Exec { dotnet pack "$ProjPath" -c Release --no-build -o "$netcoreNupkgDirectory" /p:PackageVersion=$Version }
}

Function Run-Dotnet-Mergenupkg() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$SourceNupkgPath,
        [Parameter(Mandatory=$true, Position=1)][string]$OtherNupkgPath,
        [Parameter(Mandatory=$true, Position=2)][string]$TargetFramework
    )
    try {
        Push-Location $dotnetcliToolsPath
        # restore dotnet cli tools (is not automatic like restore with dotnet build)
        Exec { dotnet restore }
        # merge nupkgs updating in place the source nupkg
        Write-Output "dotnet mergenupkg --source ""$SourceNupkgPath"" --other ""$OtherNupkgPath"" --framework $TargetFramework"
        Exec { dotnet mergenupkg --source "$SourceNupkgPath" --other "$OtherNupkgPath" --framework "$TargetFramework" }
    } finally {
        Pop-Location
    }
}

Function Get-SourceDependencies() {
    try {
        Push-Location $stagingDirectory
        if ((Test-Path "protobuf-net-read-only") -eq $false) {
            Exec { git clone https://github.com/mgravell/protobuf-net protobuf-net-read-only 2> $null }
            try {
                Push-Location protobuf-net-read-only
                Exec { git checkout 0034a10eca89471b655bdbbc8081b194ae644a2d 2> $null }
            } finally {
                Pop-Location
            }
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
Run-DotnetPack-NoBuild (Join-Path $baseDirectory "src/EventStore.ClientAPI.NetCore/EventStore.ClientAPI.csproj")
Run-Dotnet-Mergenupkg (Join-Path $outputDirectory "EventStore.Client.$Version.nupkg") (Join-Path $netcoreNupkgDirectory "EventStore.Client.$Version.nupkg") "netstandard2.0"

Run-NugetPack -NuspecPath (Join-Path $nuspecDirectory "EventStore.Client.Embedded.nuspec")
