[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Assembly version number (x.y.z.0 where x.y.z is the semantic version)")]
    [string]$Version = "0.0.0.0",
    [Parameter(HelpMessage="Configuration (Debug, Release)")]
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Release",
    [Parameter(HelpMessage="The runtime identifier")]
    [string]$Runtime = "win10-x64"
)

Function Write-Info {
    Param([string]$message)
    Process {
        Write-Host $message -ForegroundColor Cyan
    }
}

Function Build-Plugin {
	Param([string]$projectFile)

    $platform = "x64"

    Write-Info "Build Configuration"
    Write-Info "-------------------"

    Write-Info "Version: $Version"
    Write-Info "Platform: $platform"
    Write-Info "Configuration: $Configuration"
    Write-Info "Runtime Identifer: $Runtime"

    Write-Info "Publishing $($projectFile) to plugins directory"
    & dotnet publish $projectFile /p:AssemblyVersion=$Version -c $Configuration -r $Runtime /p:Platform=x64
}

Function Build-Plugins {
    $projects = Get-ChildItem -Path .\src\ -Include *.csproj -Exclude "*.Tests.*" -Recurse
    $projects | ForEach-Object {
        Build-Plugin $_
    }
}

Build-Plugins
