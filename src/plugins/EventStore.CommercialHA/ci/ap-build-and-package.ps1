[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Semantic version", Mandatory=$true)]
    [string]$SemanticVersion
)

Function Get-Assembly-Version {
[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Semantic version", Mandatory=$true)]
    [string]$SemanticVersion
)
$tokens = $SemanticVersion.Split(".-")
"$($tokens[0]).$($tokens[1]).$($tokens[2]).0"
}

Function Clean-Up {
    #Clean up everything except "packages" directory
    Push-Location $BaseDirectory
    Start-Process -Wait -NoNewWindow -FilePath "git" -ArgumentList @("reset","--hard")
    Start-Process -Wait -NoNewWindow -FilePath "git" -ArgumentList @("clean","-xdf",".","-e","packages")
    Start-Process -Wait -NoNewWindow -FilePath "git" -ArgumentList @("submodule","foreach","--recursive","git","reset","--hard")
    Start-Process -Wait -NoNewWindow -FilePath "git" -ArgumentList @("submodule","foreach","--recursive","git","clean","-xdf")
    Pop-Location
}

Function Build-Server {
    [CmdletBinding()]
    Param(
        [Parameter(HelpMessage="Semantic version", Mandatory=$true)]
        [string]$SemanticVersion,
        [Parameter(HelpMessage="Assembly version number (x.y.z.0 where x.y.z is the semantic version)", Mandatory=$true)]
        [string]$AssemblyVersion,
        [Parameter(HelpMessage="OSS/Commercial", Mandatory=$true)]
        [ValidateSet("oss","commercial")]
        [string]$BuildType,
        [Parameter(HelpMessage="Configuration (Debug, Release)", Mandatory=$true)]
        [ValidateSet("Debug","Release")]
        [string]$Configuration,
        [Parameter(HelpMessage="Runtime", Mandatory=$true)]
        [string]$Runtime,
        [Parameter(HelpMessage="Build UI (yes,no)", Mandatory=$true)]
        [ValidateSet("yes","no")]
        [string]$BuildUI
    )

    Clean-Up

    Write-Host "Building the EventStore server ($BuildType)"
    if($BuildType -eq "oss"){
        $ossPath = Join-Path $BaseDirectory "oss"
        Push-Location $ossPath > $null
        & ".\build.ps1" $AssemblyVersion $Configuration $Runtime $BuildUI
        Pop-Location > $null
    } elseif($BuildType -eq "commercial") {
        & $BaseDirectory"\build.ps1" $AssemblyVersion $Configuration $Runtime $BuildUI
    }

    Write-Host "Packaging the EventStore server ($BuildType)"
    & $BaseDirectory"\scripts\package-windows\package-windows.ps1" $SemanticVersion $BuildType $Runtime

    if($BuildType -eq "oss"){
        Write-Host "Creating Chocolatey package ($BuildType)"
        & $BaseDirectory"\scripts\package-choco\build.ps1" $SemanticVersion
    }
}

Function Build-Clients {
    [CmdletBinding()]
    Param(
        [Parameter(HelpMessage="Semantic version", Mandatory=$true)]
        [string]$SemanticVersion
    )

    Clean-Up
    Write-Host "Packaging the Client and Embedded Client"
    & $BaseDirectory"\scripts\package-clientapi\package-clientapi.ps1" $SemanticVersion    
}

#Setup parameters
$BaseDirectory = Join-Path $PSScriptRoot "..\"
$AssemblyVersion = Get-Assembly-Version $SemanticVersion
$Configuration = "Release"
$Runtime = "win10-x64"
$BuildUI = "yes"

#Create packages directory
Remove-Item -Force -Recurse "packages" -ErrorAction SilentlyContinue > $null
New-Item -ItemType Directory -Force -Path "packages" > $null

Build-Server $SemanticVersion $AssemblyVersion "oss" $Configuration $Runtime $BuildUI
# Build-Server $SemanticVersion $AssemblyVersion "commercial" $Configuration $Runtime $BuildUI
Build-Clients $SemanticVersion

Write-Host "Done!"
