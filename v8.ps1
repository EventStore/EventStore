# Event Store Build (.NET/Windows) - v8.ps1
# Use Invoke-psake ? to see further description

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host ""
    Write-Host "Event Store Build - V8"
    Write-Host "----------------------"
    Write-Host "This script is used as part of the build for the Event Store on the .NET framework"
    Write-Host "on Windows."
    Write-Host ""
    Write-Host "This file is specifically tasks and definitions related to V8."
    Write-Host ""
    Write-Host "The script uses the psake PowerShell module, which is included in the Event Store"
    Write-Host "repository but can also be found here: http://github.com/psake/psake"
    Write-Host ""
    Write-Host "The tasks in this file are not intended to be invoked directly from the command"
    Write-Host "line (although that is possible) - instead it is anticipated that these tasks will"
    Write-Host "be used by other parts of the Event Store build."
    Write-Host ""
    Write-Host "Tasks of note:"
    Write-Host ""
    Write-Host "    - Build-V8Libraries     - cleans V8 repositories, builds V8 and copies the"
    Write-Host "                              headers and libraries to the libs directory"
    Write-Host "                              **NOTE: This can take considerable time!**"
    Write-Host ""
    Write-Host "    - Get-V8AndDependencies - this will clone the V8 repository and dependendencies"
    Write-Host "                              (some of which come from Subversion). This script"
    Write-Host "                              is only intended for Windows and consequently gets"
    Write-Host "                              python and cygwin as well as V8 and Gyp."
    Write-Host "                              **NOTE: This requires network access!**"
    Write-Host "                              - Parameters:"
    Write-Host "                                   - platform      - either x86 or x64"
    Write-Host "                                   - configuration - either Debug or Release"
    Write-Host ""
}

# Directories
Properties {
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
    $patchesDirectory = Join-Path $baseDirectory "patches"

    $v8LibsDestination = Join-Path $libsDirectory $v8Platform
    $v8IncludeDestination = Join-Path $libsDirectory "include"
}

# Dependencies
Properties {
    #V8
    $v8Repository = "https://github.com/v8/v8.git"
    $v8Tag = "3.7.11"
    $v8Directory = Join-Path $baseDirectory "v8"
    
    #Python
    $pythonRepository = "http://src.chromium.org/svn/trunk/tools/third_party/python_26"
    $pythonRevision = "89111"
    $pythonDirectory = Join-Path $v8Directory (Join-Path "third_party" "python_26")
    
    #GYP
    $gypRepository = "http://gyp.googlecode.com/svn/trunk"
    $gypRevision = "1642"
    $gypDirectory = Join-Path $v8Directory (Join-Path "build" "gyp")
    
    #Cygwin
    $cygwinRepository = "http://src.chromium.org/svn/trunk/deps/third_party/cygwin"
    $cygwinRevision = "66844"
    $cygwinDirectory = Join-Path $v8Directory (Join-Path "third_party" "cygwin")
}

# Executables
Properties {
    $pythonExecutable = Join-Path $pythonDirectory "python.exe"
}

Task Build-V8Libraries -Depends Clean-V8, Build-V8, Copy-V8ToLibs

Task Get-V8AndDependencies {
    Get-GitRepoAtCommitOrTag -Verbose "V8" $v8Repository $v8Directory $v8Tag
    Get-SvnRepoAtRevision -Verbose "Python" $pythonRepository $pythonDirectory $pythonRevision
    Get-SvnRepoAtRevision -Verbose "GYP"  $gypRepository $gypDirectory $gypRevision
    Get-SvnRepoAtRevision -Verbose "CygWin" $cygwinRepository $cygwinDirectory $cygwinRevision
}

Task Clean-V8 {
    Push-Location $v8Directory
    Exec { git clean --quiet -fx -- build }
    Exec { git clean --quiet -dfx -- src }
    Exec { git clean --quiet -dfx -- test } 
    Exec { git clean --quiet -dfx -- tools }
    Exec { git clean --quiet -dfx -- preparser }
    Exec { git clean --quiet -dfx -- samples }
    Exec { git reset --quiet --hard }
    Pop-Location
}

Task Build-V8 {
    Assert ($platform -ne $null) "No platform specified. Should be either x86 or x64"
    Assert ($configuration -ne $null) "No configuration specified. Should be either Release or Debug"
    
    Push-Location $v8Directory
    Copy-Item -Container -Recurse .\third_party\gyp\ .\build\gyp\

    if ($platform -eq "x64") {
        $v8VisualStudioPlatform = "x64"
        $v8Platform = "x64"
	$v8PlatformParameter = "-Dtarget_arch=x64"
    } elseif ($platform -eq "x86") {
        $v8VisualStudioPlatform = "Win32"
        $v8Platform = ""
    } else {
        throw "Platform $platform is not supported." 
    }

    if ($configuration -eq "release") { 
        $v8VisualStudioConfiguration = "Release"
    } elseif ($configuration -eq "debug") {
        $v8VisualStudioConfiguration = "Debug"
    } else {
        throw "Configuration $configuration is not supported. If you think it should be, edit the Setup-ConfigurationParameters task to add it."
    }

    $gypFile = Join-Path $v8Directory (Join-Path "build" "gyp_v8")
    Exec { & $pythonExecutable $gypFile $v8PlatformParameter }
    Exec { msbuild .\build\all.sln /m /p:Configuration=$v8VisualStudioConfiguration /p:Platform=$v8VisualStudioPlatform }
    Pop-Location
}

Task Copy-V8ToLibs -Depends Build-V8 {

    if ($configuration -eq "release") {
        $v8VisualStudioConfiguration = "Release"
        $v8OutputDirectory = Join-Path $v8Directory (Join-Path "build" (Join-Path "Release" "lib"))
    } elseif ($configuration -eq "debug") {
        $v8VisualStudioConfiguration = "Debug"
        $v8OutputDirectory = Join-Path $v8Directory (Join-Path "build" (Join-Path "Debug" "lib"))
    } else {
        throw "Configuration $configuration is not supported. If you think it should be, edit the Setup-ConfigurationParameters task to add it."
    }

    $v8LibsSource = Join-Path $v8Directory (Join-Path "build" (Join-Path $v8OutputDirectory (Join-Path "lib" "*.lib")))
    $v8IncludeSource = Join-Path $v8Directory (Join-Path "include" "*.h")
    
    New-Item -ItemType Container -Path $v8LibsDestination -ErrorAction SilentlyContinue
    Copy-Item $v8LibsSource $v8LibsDestination -Recurse -Force -ErrorAction Stop
    New-Item -ItemType Container -Path $v8IncludeDestination -ErrorAction SilentlyContinue
    Copy-Item $v8IncludeSource $v8IncludeDestination -Recurse -Force -ErrorAction Stop

    Push-Location $v8LibsDestination
    foreach ($libFile in Get-ChildItem) {
       $newName = $libFile.Name.Replace(".x64.lib", ".lib")
       Rename-Item -Path $libFile -NewName $newName
    }
    Pop-Location
}

# Helper Functions - some of these rely on psake-provided constructs such as Exec { }.
Function Get-GitRepoAtCommitOrTag
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$dependencyName,
        [Parameter(Mandatory=$true)][string]$repositoryAddress,
        [Parameter(Mandatory=$true)][string]$localPath,
        [Parameter()][string]$commitNumberOrTagName = ""
    )
    Process {
        Write-Host "Getting dependency $dependencyName via Git"
        $localPathGitDirectory = Join-Path $localPath ".git/"
        Write-Verbose "Testing for existence of: $localPathGitDirectory"
        if ((Test-Path $localPathGitDirectory -PathType Container) -eq $true)
        {
            Write-Verbose "$localPathGitDirectory already exists - will fetch and checkout"

            if ($commitNumberOrTagName -eq "") {
                Push-Location -ErrorAction Stop -Path $localPath
                try {
                    Write-Verbose "Pulling latest changes"
                    Exec { git pull --quiet }
                } finally {
                    Pop-Location -ErrorAction Stop
                }
            } else {
                Push-Location -ErrorAction Stop -Path $localPath
                try {
                    Write-Verbose "Fetching from $repositoryAddress"
                    Exec { git fetch }
                    Write-Verbose "Checking out $commitNumberOrTagName"
                    Exec { git checkout --quiet $commitNumberOrTagName }

                } finally {
                    Pop-Location -ErrorAction Stop
                }
            }
        } else {
            Write-Verbose "$localPathGitDirectory not found"
            Write-Verbose "Cloning git repository from $repositoryAddress to $localPath"
            Exec { git clone --quiet $repositoryAddress $localPath }
            
            if ($commitNumberOrTagName -ne "") {
                Push-Location -ErrorAction Stop -Path $localPath
                try {
                    Write-Verbose "Checking out $commitNumberOrTagName"
                    Exec { git checkout --quiet $commitNumberOrTagName }
                } finally {
                    Pop-Location -ErrorAction Stop
                }
            }
        }
    }
}

Function Get-SvnRepoAtRevision
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$dependencyName,
        [Parameter(Mandatory=$true)][string]$repositoryAddress,
        [Parameter(Mandatory=$true)][string]$localPath,
        [string]$revision = ""
    )
    Process {
        Write-Host "Getting dependency $dependencyName via Subversion"
        
        if ($revision -eq "")
        {
            $revisionString = ""
            $updatingString = "Updating to latest revision"
            $checkingOutString = "Checking out svn repository from $repositoryAddress to $localPath"
        } else {
            $revisionString = "-r$revision"
            $updatingString = "Updating to revision $revision"
            $checkingOutString = "Checking out svn repository from $repositoryAddress (revision $revision) to $localPath"
        }
        
        $localPathSvnDirectory = Join-Path $localPath ".svn/"
        Write-Verbose "Testing for existence of: $localPathSvnDirectory"
        if ((Test-Path $localPathSvnDirectory -PathType Container) -eq $true)
        {
            Write-Verbose "$localPathSvnDirectory already exists"
            
            Push-Location -ErrorAction Stop -Path $localPath
            try {
                Write-Verbose $updatingString
                Exec { svn update --quiet $revisionString }
            } finally {
                Pop-Location -ErrorAction Stop
            }
        } else {
            Write-Verbose "$localPathSvnDirectory not found"
            Write-Verbose $checkingOutString
            Exec { svn checkout --quiet $revisionString $repositoryAddress $localPath }
        }
    }
}
