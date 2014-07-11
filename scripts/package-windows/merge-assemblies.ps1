# Setup and Configuration
$baseDirectory = Resolve-Path .
$binDirectory = Join-Path $baseDirectory "bin"
$mergedDirectory = Join-Path $binDirectory "merged"
$toolsDirectory = Join-Path $baseDirectory "tools"

if ((Test-Path $mergedDirectory) -eq $false) {
    New-Item -Path $mergedDirectory -ItemType Directory > $null
}

Function Merge-SingleNode
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.SingleNode.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @("js1.dll", "js1.pdb"),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore-SingleNode.exe"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName
    
    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)
    
    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Function Merge-ClusterNode
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.ClusterNode.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @("js1.dll", "js1.pdb"),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore-ClusterNode.exe"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)
    
    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Function Merge-TestClient
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.TestClient.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @(),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore-TestClient.exe"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)

    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Function Merge-PAdmin
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "PAdmin.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @(),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore-PAdmin.exe"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)

    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Function Merge-EsQuery
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "esquery.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @(),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore-Query.exe"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)

    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Function Merge-ClientAPI
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.ClientAPI.dll",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @("EventStore.ClientAPI.dll"),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = (Join-Path $toolsDirectory "ilmerge\ilmerge.exe")
    )

    # Find the build directory as a relative path to here (in case it's absolute)
    $relativeBuildDirectory = Resolve-Path -Relative -Path $BuildDirectory

    # If the executable name isn't absolute, try searching for it in the build directory
    if ((Test-Path $Executable) -eq $false) {
        $Executable = Join-Path $relativeBuildDirectory $Executable
        
        if ((Test-Path $Executable) -eq $false) {
            throw "Cannot find executable '$Executable'"
        }
    }

    # Find other assemblies to merge (with some specifically excluded for e.g. native code)
    $otherAssemblies = Get-ChildItem $relativeBuildDirectory -Filter *.dll -Exclude $ExcludeAssemblies -Name |
                       % { Join-Path $relativeBuildDirectory $_ } |
                       Join-String -Separator " "

    # Find the path of the .NET Framework DLLs
    $platformPath = (Join-Path (Get-Item 'Env:ProgramFiles(x86)').Value 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0')

    $outputName = "EventStore.ClientAPI.dll"
    $outputPath = Join-Path (Resolve-Path -Relative $OutputDirectory) $outputName

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/xmldocs", "/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)

    if ($ExcludeAssemblies.Count -gt 0) {
        Get-ChildItem -Recurse -Path $relativeBuildDirectory -Include $ExcludeAssemblies |
            Foreach-Object { Copy-Item -Force -Path $_.FullName -Destination $mergedDirectory }
    }
}

Merge-SingleNode -BuildDirectory (Join-Path $binDirectory "singlenode") -OutputDirectory $mergedDirectory
Merge-ClusterNode -BuildDirectory (Join-Path $binDirectory "clusternode") -OutputDirectory $mergedDirectory
Merge-TestClient -BuildDirectory (Join-Path $binDirectory "testclient") -OutputDirectory $mergedDirectory
Merge-PAdmin -BuildDirectory (Join-Path $binDirectory "padmin") -OutputDirectory $mergedDirectory
Merge-EsQuery -BuildDirectory (Join-Path $binDirectory "esquery") -OutputDirectory $mergedDirectory
Merge-ClientAPI -BuildDirectory (Join-Path $binDirectory "clientapi") -OutputDirectory $mergedDirectory