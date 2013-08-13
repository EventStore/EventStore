# Event Store Build (.NET/Windows) - eventstore.ps1
# Use Invoke-psake ? to see further description
 
Framework "4.0x64"
 
Task default -depends Build-Quick
 
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
    Write-Host "        Build-Quick - will fail if V8 and JS1 aren't available. Only useful for building"
    Write-Host "                      during development."
    Write-Host ""
    Write-Host "        Build-Incremental - will only build dependencies (V8, JS1) if necessary (only"
    Write-Host "                            cleans the Event Store itself)"
    Write-Host ""
    Write-Host "        Build-Full - will always build dependencies (cleans before building)"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "-----------"
    Write-Host "        version - the semantic version number to apply (e.g. 1.0.2.0). Last digit should"
    Write-Host "                  be 0 (to comply with assembly versioning rules)."
    Write-Host ""
    Write-Host "        platform - target platform. Either x64 (default) or x86."
    Write-Host ""
    Write-Host "        configuration - target configuration. Either release (default) or debug."
    Write-Host ""
    Write-Host "        platformToolset - the platform toolset to use. Default is to detect the latest"
    Write-Host "                          supported version. Valid values are v100, v110, WindowsSDK7.1"
    Write-Host ""
    Write-Host "        defines - any additional preprocessor constants to define during build (semicolon"
    Write-Host "                  separated list)."
    Write-Host ""
}
 
# Configuration
Properties {
    $nativeBuildParameters = @{}
    $managedBuildParameters = @{}
    $nativeBuildProperties = @{}
    $managedBuildProperties = @{}
     
    if ($platform -eq $null) {
        Write-Host "Platform: defaulting to x64 and Any CPU for managed"
        $platform = "x64"
        $managedBuildParameters.Add("platform", "Any CPU")
        $nativeBuildParameters.Add("platform", "x64")
    } else {
        Write-Host "Platform: set to $platform for native code"
        $managedBuildParameters.Add("platform", "Any CPU")
        $nativeBuildParameters.Add("platform", $platform)
    }
 
    if ($configuration -eq $null) {
        Write-Host "Configuration: defaulting to Release"
        $managedBuildParameters.Add("configuration", "release")
        $nativeBuildParameters.Add("configuration", "release")
    } else {
        Write-Host "Configuration: set to $configuration"
        $managedBuildParameters.Add("configuration", $configuration)
        $nativeBuildParameters.Add("configuration", $configuration)
    }
 
    if ($platformToolset -ne $null) {
        Write-Host "Platform Toolset: set to $platformToolset for native code"
        $nativeBuildParameters.Add("platformToolset", $platformToolset)
    } else {
        Write-Host "Platform Toolset will be guessed by a horrible, probably brittle mechanism recommended by MSFT support"
    }
 
    if ($version -ne $null) {
        Write-Host "Version: Set to $version"
        $nativeBuildProperties.Add("versionString", $version)
        $managedBuildProperties.Add("versionString", $version)
    } else {
        Write-Host "Version: None specified, defaulting to 0.0.0.0"
    }

    if ($defines -ne $null) {
        $managedBuildParameters.Add("defines", $defines)
        Write-Host "Additional compile-time constants: $defines"
    } else {
        Write-Host "No additional compile-time constants"
    }
 
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
    $outputDirectory = Join-Path $baseDirectory "bin\"
    $managedBuildParameters.Add("outputDirectory", $outputDirectory)
}

Task Clean-Output {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}
 
Task Build-Quick -Depends Clean-Output {
    $hasDependencies = (Test-Path (Join-Path $libsDirectory (Join-Path $platform "js1.dll")))
    if ($hasDependencies) {
        Write-Host "Re-using JS1.dll from a previous build - it is likely to have the wrong commit hash!" -ForegroundColor Yellow
        Invoke-psake .\eventstore.ps1 Build-EventStore -parameters $managedBuildParameters -properties $managedBuildProperties -Verbose
    } else {
        throw "Build-Quick can only be used if a full or incremental build has build JS1.dll and it is in the libs directory"
    }
}
 
Task Build-Incremental -Depends Clean-Output {
 
    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies -Verbose
    }
 
    Invoke-psake .\native-code.ps1 Build-NativeIncremental -parameters $nativeBuildParameters -properties $nativeBuildProperties -Verbose
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters $managedBuildParameters -properties $managedBuildProperties -Verbose
}
 
Task Build-Full -Depends Clean-Output {
 
    if (Test-Dependencies -eq $false)
    {
        Invoke-psake .\dependencies.ps1 Get-Dependencies -Verbose
    }
 
    Invoke-psake .\native-code.ps1 Build-NativeFull -parameters $nativeBuildParameters -properties $nativeBuildProperties -Verbose
    Invoke-psake .\eventstore.ps1 Build-EventStore -parameters $managedBuildParameters  -properties $managedBuildProperties -Verbose
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