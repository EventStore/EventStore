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
    Write-Host "IMPORTANT: We guess about which platform toolset to use based on observation of"
    Write-Host "where various directories are when VS2012, VS2010 or the Windows SDK 7.1 are installed."
    Write-Host "If you don't like our guess, pass in the platformToolset parameter."
    Write-Host ""
    Write-Host "Tasks of note:"
    Write-Host ""
    Write-Host "    - Build-V8Libraries     - cleans V8 repositories, builds V8 and copies the"
    Write-Host "                              headers and libraries to the libs directory"
    Write-Host "                              **NOTE: This can take considerable time!**"
    Write-Host ""
    Write-Host "                              - Parameters:"
    Write-Host "                                - (required) platform      - either x86 or x64"
    Write-Host "                                - (required) configuration - either Debug or Release"
    Write-Host "                                - (optional) platformToolset - v110, v100, or Windows7.1SDK"
    Write-Host ""
    Write-Host "    - Get-V8AndDependencies - this will clone the V8 repository and dependendencies"
    Write-Host "                              (some of which come from Subversion). This script"
    Write-Host "                              is only intended for Windows and consequently gets"
    Write-Host "                              python and cygwin as well as V8 and Gyp."
    Write-Host "                              **NOTE: This requires network access!**"
    Write-Host ""
}

# Directories
Properties {
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
}

# Dependencies
Properties {
    #V8
    $v8Repository = "https://github.com/v8/v8.git"
    $v8Tag = "3.19.7"
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
    $commonGypiPath = Join-Path $v8Directory (Join-Path "build" "common.gypi")
    $includeParameter = "-I$commonGypiPath"

    if ($platformToolset -eq $null) {
        $platformToolset = Get-BestGuessOfPlatformToolsetOrDie($v8VisualStudioPlatform)
    }

    Exec { & $pythonExecutable $gypFile $includeParameter $v8PlatformParameter }
    Exec { msbuild .\build\all.sln /m /p:Configuration=$v8VisualStudioConfiguration /p:Platform=$v8VisualStudioPlatform /p:PlatformToolset=$platformToolset }
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

    if ($platform -eq "x64") {
	    $v8LibsDestination = Join-Path $libsDirectory "x64"
    } elseif ($platform -eq "x86") {
	    $v8LibsDestination = Join-Path $libsDirectory "Win32"
    } else {
	    throw "Configuration $configuration is not supported."
    }

    $v8IncludeDestination = Join-Path $libsDirectory "include"
    
    $v8LibsSource = Join-Path $v8OutputDirectory "*.lib"
    $v8IncludeSource = Join-Path $v8Directory (Join-Path "include" "*.h")
    
    New-Item -ItemType Container -Path $v8LibsDestination -ErrorAction SilentlyContinue
    Copy-Item $v8LibsSource $v8LibsDestination -Recurse -Force -ErrorAction Stop
    New-Item -ItemType Container -Path $v8IncludeDestination -ErrorAction SilentlyContinue
    Copy-Item $v8IncludeSource $v8IncludeDestination -Recurse -Force -ErrorAction Stop

    Push-Location $v8LibsDestination

    #V8 build changed at some point to include the platform in the
    # name of the lib file. Where we use V8 we still use the old names
    # so rename here if necessary.
    foreach ($libFile in Get-ChildItem) {
        $newName = $libFile.Name.Replace(".x64.lib", ".lib")
        if ($newName -ne $libFile.Name) {
            if (Test-Path $newName) {
                Remove-Item $newName -Force
            }
            Rename-Item -Path $libFile -NewName $newName
        }
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