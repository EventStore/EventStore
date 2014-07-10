Function Merge-SingleNode
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.SingleNode.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @("js1.dll"),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = "..\..\tools\ilmerge\IlMerge.exe"
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
    if ((Test-Path $OutputDirectory) -eq $false) {
        New-Item -Path $OutputDirectory -ItemType Directory
    }

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)
}

Function Merge-ClusterNode
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BuildDirectory,
        [Parameter(Mandatory=$true)][string]$OutputDirectory,
        [Parameter(Mandatory=$false)][string]$Executable = "EventStore.ClusterNode.exe",
        [Parameter(Mandatory=$false)][string[]]$ExcludeAssemblies = @("js1.dll"),
        [Parameter(Mandatory=$false)][string]$IlMergeToolPath = "..\..\tools\ilmerge\IlMerge.exe"
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
    if ((Test-Path $OutputDirectory) -eq $false) {
        New-Item -Path $OutputDirectory -ItemType Directory
    }

    Start-Process -Wait -NoNewWindow -FilePath $IlMergeToolPath -ArgumentList @("/internalize", "/targetPlatform:v4,""$platformPath""", "/out:$outputPath", $Executable, $otherAssemblies)
}

#Merge-SingleNode -BuildDirectory ..\..\bin\singlenode -OutputDirectory ..\..\bin\merged\
#Merge-ClusterNode -BuildDirectory ..\..\bin\clusternode -OutputDirectory ..\..\bin\merged\