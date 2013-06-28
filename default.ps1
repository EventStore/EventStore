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
    Write-Host "        Build-Incremental"
    Write-Host ""
    Write-Host "        Build-Full"
    Write-Host ""
}

# Configuration
Properties {
    if ($platform -eq $null) {
        Write-Verbose "Platform: defaulting to X64"
        $platform = "x64"
    } else {
        Write-Verbose "Platform: set to $platform"
    }

    if ($configuration -eq $null) {
        Write-Verbose "Configuration: defaulting to Release"
        $configuration = "release"
    } else {
        Write-Verbose "Configuration: set to $configuration"
    }
}

# Paths
Properties {
    $baseDirectory = Resolve-Path .
    $outputDirectory = Join-Path $baseDirectory "bin\"
}

Task Clean-Output {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}

Task Build-Incremental -Depends Clean-Output {

    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies
    }

    Invoke-psake .\native-code.ps1 Build-NativeIncremental -parameters @{ 'platform' = "$platform" ; 'configuration' = "$configuration" }
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters @{ 'platform' = "$platform" ; 'configuration' = "$configuration" ; 'outputDirectory' = "$outputDirectory" }
}

Task Build-Full {

    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies
    }

    Invoke-psake .\native-code.ps1 Build-Full -parameters @{ 'platform' = "$platform" ; 'configuration' = "$configuration" }
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters @{ 'platform' = "$platform" ; 'configuration' = "$configuration" }
}

#--------------------------------------------------------------------------

Function Test-Dependencies
{
    # This is a derp, using exceptions for flow control. If you can think
    # of a better way for this, pull request plz.
    try {
        Invoke-psake .\dependencies.ps1 Test-Dependencies
        return $true
    } catch {
        return $false
    }
}
