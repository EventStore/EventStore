# Event Store Build (.NET/Windows) - default.ps1
# Use Invoke-psake ? to see further description

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host ""
    Write-Host "Event Store Build - Main Script"
    Write-Host "-------------------------------"
    Write-Host "This script is used as part of the build for the Event Store on the .NET framework"
    Write-Host "on Windows."
    Write-Host ""
    Write-Host "This file contains the main build definitions."
    Write-Host ""
    Write-Host "The script uses the psake PowerShell module, which is included in the Event Store"
    Write-Host "repository but can also be found here: http://github.com/psake/psake"
    Write-Host ""
    Write-Host "IMPORTANT: We default to building X64 in Release mode. Platform and Configuration"
    Write-Host "allow this to be overriden."
    Write-Host ""
    Write-Host "IMPORTANT: We guess about which platform toolset to use based on observation of"
    Write-Host "where various directories are when VS2012, VS2010 or the Windows SDK 7.1 are installed."
    Write-Host "If you don't like our guess, pass in the platformToolset parameter."
    Write-Host ""
    Write-Host "Tasks of note:"
    Write-Host ""
}

# Directories
Properties {
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
    $outputDirectory = Join-Path $baseDirectory "bin"
}

# Project Files
Properties {
    $js1Project = Join-Path $srcDirectory (Join-Path "EventStore.Projections.v8Integration" "EventStore.Projections.v8Integration.vcxproj")
    $eventStoreSolution = Join-Path $srcDirectory "EventStore.sln"
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

Task Clean-EventStore {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}

Task Clean-Libs {
    Push-Location $libsDirectory
    Exec { git clean --quiet -xdf }
    Pop-Location
}

Task Build-js1 {    
    if ($platform -eq "x64") {
        $js1VisualStudioPlatform = "x64"
    } elseif ($platform -eq "x86") {
        $js1VisualStudioPlatform = "Win32"
    } else {
        throw "Platform $platform is not supported." 
    }

    if ($configuration -eq "release") { 
        $js1VisualStudioConfiguration = "Release"
    } elseif ($configuration -eq "debug") {
        $js1VisualStudioConfiguration = "Debug"
    } else {
        throw "Configuration $configuration is not supported."
    }

    if ($platformToolset -eq $null) {
        $platformToolset = Get-BestGuessOfPlatformToolsetOrDie($js1VisualStudioPlatform)
        Write-Verbose "PlatformToolset: Determined to be $platformToolset"
    } else {
        Write-Verbose "PlatformToolset: Set to $platformToolset"
    }

    Exec { msbuild $js1Project /p:Configuration=$js1VisualStudioConfiguration /p:Platform=$js1VisualStudioPlatform /p:PlatformToolset=$platformToolset }
}

Task Build-EventStore {
    Exec { msbuild $eventStoreSolution /p:Configuration=$configuration /p:Platform="Any CPU" /p:OutDir=$outputDirectory }
}


# Helper Functions - some of these rely on psake-provided constructs such as Exec { }.

Function Get-BestGuessOfPlatformToolsetOrDie {
    [CmdletBinding()]
    Param(
        [Parameter()][string]$platform = "x64"
    )
    Process {
        if (Test-Path 'Env:\ProgramFiles(x86)') {
            $programFiles = ${env:ProgramFiles(x86)}
        } else {
            $programFiles = ${env:ProgramFiles}
        }

        $mscppDir = Join-Path $programFiles (Join-Path "MSBuild" (Join-Path "Microsoft.Cpp" "v4.0"))

        Assert (Test-Path $mscppDir) "$mscppDir does not exist. It appears this machine either does not have MSBuild and C++ installed, or it's in a weird place. Specify the platform toolset manually as a parameter."

        #We'll prefer to use the V110 toolset if it's available
        $potentialV110Dir = Join-Path $mscppDir "V110"
        if (Test-Path $potentialV110Dir) {
            return "V110"
        }

        #Failing that, we'll have to look inside a platform to figure out which ones are there
        $platformToolsetsDir = Join-Path $mscppDir (Join-Path "Platforms" (Join-Path $platform "PlatformToolsets"))

        Assert (Test-Path $platformToolsetsDir) "Neither a V110 directory not a Platforms directory exists. Specify the platform toolset manually as a parameter."

        #If we have Windows7.1SDK we'll take that, otherwise we'll assume V100
        if (Test-Path (Join-Path $platformToolsetsDir "Windows7.1SDK")) {
            return "Windows7.1SDK"
        } elseif (Test-Path (Join-Path $platformToolsetsDir "V100")) { 
            return "V100"
        } else {
            Assert ($false) "Can't find any supported platform toolset (V100, V110, Windows7.1SDK). It's possible that this detection is wrong, in which case you should specify the platform toolset manually as a parameter."
        }
    }
}