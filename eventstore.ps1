# Event Store Build (.NET/Windows) - eventstore.ps1
# Use Invoke-psake ? to see further description

Framework "4.0x64"

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host "Builds the managed part of the Event Store"
}

# Version and other metadata
Properties {
    $versionString = "0.0.0.0"
    $productName = "Event Store Open Source"
    $companyName = "Event Store LLP"
    $copyright = "Copyright 2012 Event Store LLP. All rights reserved."
    $commitHash = Get-GitCommitHash
    $branchName = Get-GitBranchOrTag
}

# Directories
Properties {
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
    $outputDirectory = Join-Path $baseDirectory "bin\"
}

# Project Files
Properties {
    $eventStoreSolution = Join-Path $srcDirectory "EventStore.sln"
}

Task Build-EventStore {
    try {
        Invoke-Task Patch-AssemblyInfos
        Exec { msbuild $eventStoreSolution /p:Configuration=$configuration /p:Platform="Any CPU" /p:OutDir=$outputDirectory }
    } finally {
        Invoke-Task Revert-AssemblyInfos
    }
}

Task Patch-AssemblyInfos {
    Push-Location $baseDirectory
    $assemblyInfos = Get-ChildItem -Recurse -Filter AssemblyInfo.cs
    foreach ($assemblyInfo in $assemblyInfos) {
        $path = Resolve-Path $assemblyInfo.FullName -Relative
        Write-Verbose "Patching $path with product information."
        Patch-AssemblyInfo $path $versionString $versionString $branchName $commitHash $productName $companyName $copyright
    }
    Pop-Location
}

Task Revert-AssemblyInfos {
    Push-Location $baseDirectory
    $assemblyInfos = Get-ChildItem -Recurse -Filter AssemblyInfo.cs
    foreach ($assemblyInfo in $assemblyInfos) {
        $path = Resolve-Path $assemblyInfo.FullName -Relative
        Write-Verbose "Reverting $path to original state."
        & { git checkout --quiet $path }
    }
    Pop-Location
}

#Helper functions
Function Get-GitCommitHash
{
    $lastCommitLog = Exec { git log --max-count=1 --pretty=oneline } "Cannot execute git log. Ensure that the current directory is a git repository and that git is available on PATH."
    return $lastCommitLog.Split(' ')[0]
}

Function Get-GitBranchOrTag
{
    $revParse = Exec { git rev-parse --abbrev-ref HEAD } "Cannot execute git rev-parse. Ensure that the current directory is a git repository and that git is available on PATH."
    if ($revParse -ne "HEAD") {
        return $revParse
    }

    $describeTags = Exec { git describe --tags } "Cannot execute git describe. Ensure that the current directory is a git repository and that git is available on PATH."
    return $describeTags
}

Function Patch-AssemblyInfo {
    Param(
        [Parameter(Mandatory=$true)][string]$assemblyInfoFilePath,
        [Parameter(Mandatory=$true)][string]$version,
        [Parameter(Mandatory=$true)][string]$fileVersion,
        [Parameter(Mandatory=$true)][string]$branch,
        [Parameter(Mandatory=$true)][string]$commitHash,
        [Parameter(Mandatory=$true)][string]$productName,
        [Parameter(Mandatory=$true)][string]$companyName,
        [Parameter()][string]$copyright
    )
    Process {
        $newAssemblyVersion = 'AssemblyVersion("' + $version + '")'
        $newAssemblyFileVersion = 'AssemblyFileVersion("' + $fileVersion + '")'
        $newAssemblyVersionInformational = 'AssemblyInformationalVersion("' + $version + ' - ' + $branch + ' - ' + $commitHash + '")'
        $newAssemblyProductName = 'AssemblyProduct("' + $productName + '")'
        $newAssemblyCopyright = 'AssemblyCopyright("'+ $copyright + '")'
        $newAssemblyCompany = 'AssemblyCompany("' + $companyName + '")'
        
        $assemblyVersionPattern = 'AssemblyVersion\(".*"\)'
        $assemblyFileVersionPattern = 'AssemblyFileVersion\(".*"\)'
        $assemblyVersionInformationalPattern = 'AssemblyInformationalVersion\(".*"\)'
        $assemblyProductNamePattern = 'AssemblyProduct\(".*"\)'
        $assemblyCopyrightPattern = 'AssemblyCopyright\(".*"\)'
        $assemblyCompanyPattern = 'AssemblyCompany\(".*"\)'

        $edited = (Get-Content $assemblyInfoFilePath) | ForEach-Object {
            % {$_ -replace "\/\*+.*\*+\/", "" } |
            % {$_ -replace "\/\/+.*$", "" } |
            % {$_ -replace "\/\*+.*$", "" } |
            % {$_ -replace "^.*\*+\/\b*$", "" } |
            % {$_ -replace $assemblyVersionPattern, $newAssemblyVersion } |
            % {$_ -replace $assemblyFileVersionPattern, $newAssemblyFileVersion } |
            % {$_ -replace $assemblyVersionInformationalPattern, $newAssemblyVersionInformational } |
            % {$_ -replace $assemblyProductNamePattern, $newAssemblyProductName } |
            % {$_ -replace $assemblyCopyrightPattern, $newAssemblyCopyright } |
            % {$_ -replace $assemblyCompanyPattern, $newAssemblyCompany }
        }

        if (!(($edited -match $assemblyVersionInformationalPattern) -ne "")) {
            $edited += "[assembly: $newAssemblyVersionInformational]"
        }

        Set-Content -Path $assemblyInfoFilePath -Value $edited
    }
}