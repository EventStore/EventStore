# Event Store Build (.NET/Windows) - eventstore.ps1
# Use Invoke-psake ? to see further description

Framework "4.0x64"

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host ""
    Write-Host "Event Store Build - Main Script"
    Write-Host "-------------------------------"
    Write-Host ""
    Write-Host "This script is used as part of the build for the Event Store on the .NET framework"
    Write-Host "on Windows."
    Write-Host ""
    Write-Host "The script uses the psake PowerShell module, which is included in the Event Store"
    Write-Host "repository but can also be found here: http://github.com/psake/psake"
    Write-Host ""
    Write-Host "IMPORTANT: We default to building X64 in Release mode. The Platform and Configuration"
    Write-Host "parameters allow this to be overriden."
    Write-Host ""
    Write-Host "IMPORTANT: We guess about which platform toolset to use based on observation of"
    Write-Host "where various directories are when VS2012, VS2010 or the Windows SDK 7.1 are installed."
    Write-Host "If you don't like our guess, pass in the platformToolset parameter with the same value"
    Write-Host "as you'd pass to the C++ compiler (e.g. v110, v100 etc)."
    Write-Host ""
    Write-Host "Tasks:"
    Write-Host "------"
    Write-Host ""
    Write-Host "        Build-Incremental - will only build dependencies (V8, JS1) if necessary (only"
    Write-Host "                            cleans the Event Store itself)"
    Write-Host ""
    Write-Host "        Build-Full - will always build dependencies (cleans before building)"
    Write-Host ""
}

# Configuration
Properties {
    $nativeBuildParameters = @{}
    $managedBuildParameters = @{}

    if ($platform -eq $null) {
        Write-Verbose "Platform: defaulting to x64 and Any CPU for managed"
        $managedBuildParameters.Add("platform", "Any CPU")
        $nativeBuildParameters.Add("platform", "x64")
    } else {
        Write-Verbose "Platform: set to $platform"
        $managedBuildParameters.Add("platform", $platform)
        $nativeBuildParameters.Add("platform", $platform)
    }

    if ($configuration -eq $null) {
        Write-Verbose "Configuration: defaulting to Release"
        $managedBuildParameters.Add("configuration", "release")
        $nativeBuildParameters.Add("configuration", "release")
    } else {
        Write-Verbose "Configuration: set to $configuration"
        $managedBuildParameters.Add("configuration", $configuration)
        $nativeBuildParameters.Add("configuration", $configuration)
    }

    if ($platformToolset -ne $null) {
        Write-Verbose "Platform Toolset: set to $platformToolset for native code"
        $nativeBuildParameters.Add("platformToolset", $platformToolset)
    } else {
        Write-Verbose "Platform Toolset will be guessed by a horrible, probably brittle mechanism recommended by MSFT support"
    }

    $baseDirectory = Resolve-Path .
    $outputDirectory = Join-Path $baseDirectory "bin\"
    $managedBuildParameters.Add("outputDirectory", $outputDirectory)
}

Task Clean-Output {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}

Task Build-Incremental -Depends Clean-Output {

    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies -Verbose
    }

    Invoke-psake .\native-code.ps1 Build-NativeIncremental -parameters $nativeBuildParameters -Verbose
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters $managedBuildParameters -Verbose
}

Task Build-Full {

    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies -Verbose
    }

    Invoke-psake .\native-code.ps1 Build-Full -parameters $nativeBuildParameters -Verbose
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters $managedBuildParameters -Verbose
}

#--------------------------------------------------------------------------

# Helper Functions

Function Test-Dependencies
{
    # This is a derp, using exceptions for flow control. If you can think
    # of a better way for this, pull request plz.
    try {
        Invoke-psake .\dependencies.ps1 Test-Dependencies -Verbose
        return $true
    } catch {
        return $false
    }
}
