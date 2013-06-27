# Event Store Build (.NET/Windows) - eventstore.ps1
# Use Invoke-psake ? to see further description

Framework "4.0x64"

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host "Builds the managed part of the Event Store"
}

# Version
Properties {
    $versionString = "2.0.0-rc1"
    $productName = "Event Store Open Source"
}

# Static Product Info
Properties {
    $description = "Event Store"
    $copyright = "Copyright © 2012-2013 Event Store LLP. All rights reserved."
    $company = "Event Store LLP"
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
    $eventStoreSolution = Join-Path $srcDirectory "EventStore.sln"
}

Task Clean-EventStore {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}

Task Clean-Libs {
    Push-Location $libsDirectory
    Exec { git clean --quiet -xdf }
    Pop-Location
}

Task Build-EventStore {
    Exec { msbuild $eventStoreSolution /p:Configuration=$configuration /p:Platform="Any CPU" /p:OutDir=$outputDirectory }
}

#Task Update-AssemblyInfos {
#    Get-ChildItem -Recurse -Filter AssemblyInfo.cs | % {
#        & { stext $_.FullName }
#    }
#}

#Helper functions

Function Generate-AssemblyInfoCSharp
{
    [CmdletBinding()]
    Param(
        [string]$title, 
        [string]$description, 
        [string]$company, 
        [string]$product, 
        [string]$copyright, 
        [string]$version,
        [string]$sourceControlReference,
        [string]$commit,
        [string]$clsCompliant = "true",
        [Parameter(Mandatory=$true)][string]$file
    )
    Process {
        $assemblyInfo = "using System;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitleAttribute(""$title"")]
[assembly: AssemblyDescriptionAttribute(""$description"")]
[assembly: AssemblyCompanyAttribute(""$company"")]
[assembly: AssemblyProductAttribute(""$product"")]
[assembly: AssemblyCopyrightAttribute(""$copyright"")]
[assembly: AssemblyVersionAttribute(""$version"")]
[assembly: AssemblyInformationalVersionAttribute(""$version - $sourceControlReference@$commit"")]
[assembly: AssemblyDelaySignAttribute(false)]
[assembly: CLSCompliantAttribute($clsCompliant )]
[assembly: ComVisibleAttribute(false)]
"

        Write-Verbose "Generating AssemblyInfo file: $file"
        New-Item -Force -ItemType File -Path $file -Value $assemblyInfo
    }
}