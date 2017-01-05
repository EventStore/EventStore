﻿#Functions

#Make compatible with Powershell 2
if(!$PSScriptRoot) { $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent }

$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$toolsDirectory = Join-Path $baseDirectory "tools"
$svnClientPath = Join-Path $toolsDirectory (Join-Path "svn" "svn.exe")

Function Write-Info {
    Param([string]$message)
    Process {
        Write-Host $message -ForegroundColor Cyan
    }
}

#Borrowed from psake
Function Exec
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)][scriptblock]$Command,
        [Parameter(Mandatory=$false, Position=1)][string]$ErrorMessage = ("Failed executing {0}" -F $Command)
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw ("Exec: " + $ErrorMessage)
    }
}

# Borrowed from psake
Function Assert
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]$ConditionToCheck,
        [Parameter(Mandatory=$true, Position=1)]$FailureMessage
    )
    if (!$ConditionToCheck) {
        throw ("Assert: " + $FailureMessage)
    }
}

Function Test-DirectoryIsJunctionPoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [ValidateScript({Test-Path $_ -PathType Container})]
        [string]$Path
    )
    Process {
        return (Get-Item $Path).Attributes.ToString().Contains("ReparsePoint")
    }
}

Function Test-ShouldTryNetworkAccess {
    #Opaque GUID from - http://blogs.microsoft.co.il/blogs/scriptfanatic/archive/2010/03/09/quicktip-how-do-you-check-internet-connectivity.aspx
    $hasNetwork = [Activator]::CreateInstance([Type]::GetTypeFromCLSID([Guid]'{DCB00C01-570F-4A9B-8D69-199FDBA5723B}')).IsConnectedToInternet
    return ($hasNetwork -eq $true) -or ($forceNetwork -eq $true)
}

Function Test-SvnRepoIsAtRevision {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$WorkingCopy,
        [Parameter(Mandatory=$true)][string]$Revision
    )
    Process {
        try {
            if ((Test-Path $WorkingCopy) -eq $false) {
                return $false
            }
            Push-Location $WorkingCopy
            $svnCommand = "$svnClientPath info --xml"
            [xml]$svnInfo = Exec ([ScriptBlock]::Create($svnCommand))
            $actualRevision = $svnInfo.info.entry.GetAttribute("revision")
            return ($actualRevision -eq $Revision)
        } catch {
            return $false
        } finally {
            Pop-Location
        }
    }
}

# These utility functions have been extracted from the Pscx.Utility.psm1 module
# of the Powershell Community Extensions, which is here: http://pscx.codeplex.com/
Function Invoke-BatchFile
{
    Param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path,
        [Parameter(Mandatory = $false, Position = 1)]
        [string]$Parameters,
        [Parameter(Mandatory = $false, Position = 2)]
        [bool]$RedirectStdErrToNull = $false
    )

    End {
        $tempFile = [IO.Path]::GetTempFileName()

        ## Store the output of cmd.exe.  We also ask cmd.exe to output
        ## the environment table after the batch file completes
        if ($RedirectStdErrToNull -eq $true) {
            (cmd.exe /c " `"$Path`" $Parameters && set > `"$tempFile`" ") 2> $null
        } else {
            cmd.exe /c " `"$Path`" $Parameters && set > `"$tempFile`" "
        }

        ## Go through the environment variables in the temp file.
        ## For each of them, set the variable in our local environment.
        Get-Content $tempFile | Foreach-Object {
            if ($_ -match "^(.*?)=(.*)$")
            {
                Set-Content "env:\$($matches[1])" $matches[2]
            }
        }

        Remove-Item $tempFile
    }
}

Function Import-VisualStudioVars
{
    Param
    (
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateSet('2010', '2012', '2013', '2015', 'Windows7.1SDK')]
        [string]$VisualStudioVersion,
        [Parameter(Position = 1)]
        [string]$Architecture = 'amd64',
        [Parameter(Position = 2)]
        [string]$Configuration = 'release'
    )

    End
    {
        switch ($VisualStudioVersion)
        {
            '2010' {
                Push-Environment
                Invoke-BatchFile (Join-Path $env:VS100COMNTOOLS "..\..\VC\vcvarsall.bat") -Parameters $Architecture -RedirectStdErrToNull $false
            }

            '2012' {
                Push-Environment
                Invoke-BatchFile (Join-Path $env:VS110COMNTOOLS "..\..\VC\vcvarsall.bat") -Parameters $Architecture -RedirectStdErrToNull $false
            }

            '2013' {
                Push-Environment
                Invoke-BatchFile (Join-Path $env:VS120COMNTOOLS "..\..\VC\vcvarsall.bat") -Parameters $Architecture -RedirectStdErrToNull $false
            }
            
            '2015' {
                Push-Environment
                Invoke-BatchFile (Join-Path $env:VS140COMNTOOLS "VsDevCmd.bat") -Parameters $Architecture -RedirectStdErrToNull $false
            }

            'Windows7.1SDK' {
                if ($Architecture -eq "amd64") {
                    $architectureParameter = "/x64"
                } elseif ($Architecture -eq "x86") {
                    $architectureParameter = "/x86"
                } else {
                    Write-Host "Unknown Configuration: $configuration"
                    return
                }

                if ($Configuration -eq "release") {
                    $configurationParameter = "/release"
                } elseif ($configuration -eq "debug") {
                    $configurationParameter = "/debug"
                } else {
                    Write-Host "Unknown Configuration: $configuration"
                    return
                }

                Push-Environment
                Invoke-BatchFile (Join-Path $env:ProgramFiles "Microsoft SDKs\Windows\v7.1\Bin\setenv.cmd") -Parameters "$configurationParameter $architectureParameter" -RedirectStdErrToNull $true
            }

            default {
                Write-Error "Import-VisualStudioVars doesn't recognize VisualStudioVersion: $VisualStudioVersion"
            }
        }
    }
}

Function Get-GuessedVisualStudioVersion {
    #Platform SDK (since it seems to set VS100COMNTOOLS even without Visual Studio 2010 installed)
    if (Test-Path (Join-Path $env:ProgramFiles "Microsoft SDKs\Windows\v7.1\Bin\setenv.cmd")) {
        return 'Windows7.1SDK'
    }

    #Visual Studio's, newest versions first

    #Visual Studio 2015
    if ((Test-Path env:\VS140COMNTOOLS) -and (Test-Path (Join-Path $env:VS140COMNTOOLS "VsMSBuildCmd.bat"))) {
        return '2015'
    }

    #Visual Studio 2013
    if ((Test-Path env:\VS120COMNTOOLS) -and (Test-Path (Join-Path $env:VS120COMNTOOLS "..\..\VC\vcvarsall.bat"))) {
        return '2013'
    }

    #Visual Studio 2012
    if ((Test-Path env:\VS110COMNTOOLS) -and (Test-Path (Join-Path $env:VS110COMNTOOLS "..\..\VC\vcvarsall.bat"))) {
        return '2012'
    }

    #Visual Studio 2010
    if ((Test-Path env:\VS100COMNTOOLS) -and (Test-Path (Join-Path $env:VS100COMNTOOLS "..\..\VC\vcvarsall.bat"))) {
        return '2010'
    }

    throw "Can't find any of VS2010-2015 or WindowsSDK7.1."
}

Function Get-PlatformToolsetForVisualStudioVersion {
    Param(
        [Parameter(Mandatory=$true)]
        [string]$VisualStudioVersion
    )
    Process {
        if  ($VisualStudioVersion -eq "2015") {
            return "v140"
        } elseif($VisualStudioVersion -eq "2013") {
            return "v120"
        } elseif($VisualStudioVersion -eq "2012") {
            return "v110"
        } elseif($VisualStudioVersion -eq "2010") {
            return "V100"
        } elseif($VisualStudioVersion -eq "Windows7.1SDK") {
            return "Windows7.1SDK"
        } else {
            throw "Can'find the platform toolset for Visual Studio version $VisualStudioVersion"
        }
    }
}


Function Get-GitCommitHashAndTimestamp
{
    $lastCommitLog = Exec { git log --max-count=1 --pretty=format:%H@%aD HEAD } "Cannot execute git log. Ensure that the current directory is a git repository and that git is available on PATH."
    return $lastCommitLog
}

Function Get-GitCommitHash
{
    $lastCommitLog = Exec { git log --max-count=1 --pretty=format:%H HEAD } "Cannot execute git log. Ensure that the current directory is a git repository and that git is available on PATH."
    return $lastCommitLog
}

Function Get-GitTimestamp
{
    $lastCommitLog = Exec { git log --max-count=1 --pretty=format:%aD HEAD } "Cannot execute git log. Ensure that the current directory is a git repository and that git is available on PATH."
    return $lastCommitLog
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

Function Patch-CppVersionResource {
    Param(
        [Parameter(Mandatory=$true)]
        [string]$versionResourcePath,
        [Parameter(Mandatory=$true)]
        [string]$version,
        [Parameter(Mandatory=$true)]
        [string]$fileVersion,
        [Parameter(Mandatory=$true)]
        [string]$branch,
        [Parameter(Mandatory=$true)]
        [string]$commitHashAndTimestamp,
        [Parameter(Mandatory=$true)]
        [string]$productName,
        [Parameter(Mandatory=$true)]
        [string]$companyName,
        [Parameter()][string]$copyright
    )
    Process {
        $separatedVersion = $version -replace "\.", ","
        $separatedFileVersion = $fileVersion -replace "\.", ","

        $newProductNameStr = '#define EVENTSTORE_PRODUCTNAME_STR "' + $productName + '"'
        $newProductVersionStr = '#define EVENTSTORE_PRODUCTVERSION_STR "' + $version + '"'
        $newProductVersion = '#define EVENTSTORE_PRODUCTVERSION ' + $separatedVersion
        $newFileVersionStr = '#define EVENTSTORE_FILEVERSION_STR "' + $fileVersion + '"'
        $newFileVersion = '#define EVENTSTORE_FILEVERSION ' + $separatedFileVersion
        $newCommitNumberStr = '#define EVENTSTORE_COMMITNUMBER_STR "' + $version + '.' + $branch + '@' + $commitHashAndTimestamp + '"'
        $newCopyrightStr = '#define EVENTSTORE_COPYRIGHT_STR "' + $copyright + '"'

        $newProductNameStrPattern = '#define EVENTSTORE_PRODUCTNAME_STR.*$'
        $newProductVersionStrPattern = '#define EVENTSTORE_PRODUCTVERSION_STR.*$'
        $newProductVersionPattern = '#define EVENTSTORE_PRODUCTVERSION .*$'
        $newFileVersionStrPattern = '#define EVENTSTORE_FILEVERSION_STR.*$'
        $newFileVersionPattern = '#define EVENTSTORE_FILEVERSION .*$'
        $newCommitNumberStrPattern = '#define EVENTSTORE_COMMITNUMBER_STR.*$'
        $newCopyrightStrPattern = '#define EVENTSTORE_COPYRIGHT_STR.*$'


        $edited = (Get-Content $versionResourcePath) | ForEach-Object {
            % {$_ -replace "\/\*+.*\*+\/", "" } |
            % {$_ -replace "\/\/+.*$", "" } |
            % {$_ -replace "\/\*+.*$", "" } |
            % {$_ -replace "^.*\*+\/\b*$", "" } |
            % {$_ -replace $newProductNameStrPattern, $newProductNameStr } |
            % {$_ -replace $newProductVersionStrPattern, $newProductVersionStr } |
            % {$_ -replace $newProductVersionPattern, $newProductVersion } |
            % {$_ -replace $newFileVersionStrPattern, $newFileVersionStr } |
            % {$_ -replace $newFileVersionPattern, $newFileVersion } |
            % {$_ -replace $newCommitNumberStrPattern, $newCommitNumberStr } |
            % {$_ -replace $newCopyrightStrPattern, $newCopyrightStr }
        }

        Set-Content -Path $versionResourcePath -Value $edited
    }
}

Function Patch-AssemblyInfo {
    Param(
        [Parameter(Mandatory=$true)]
        [string]$assemblyInfoFilePath,
        [Parameter(Mandatory=$true)]
        [string]$version,
        [Parameter(Mandatory=$true)]
        [string]$fileVersion,
        [Parameter(Mandatory=$true)]
        [string]$branch,
        [Parameter(Mandatory=$true)]
        [string]$commitHashAndTimestamp,
        [Parameter(Mandatory=$true)]
        [string]$productName,
        [Parameter(Mandatory=$true)]
        [string]$companyName,
        [Parameter()][string]$copyright
    )
    Process {
        $newAssemblyVersion = 'AssemblyVersion("' + $version + '")'
        $newAssemblyFileVersion = 'AssemblyFileVersion("' + $fileVersion + '")'
        $newAssemblyVersionInformational = 'AssemblyInformationalVersion("' + $version + '.' + $branch + '@' + $commitHashAndTimestamp + '")'
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

Function Patch-VersionInfo {
    Param(
        [Parameter(Mandatory=$true)]
        [string]$versionInfoFilePath,
        [Parameter(Mandatory=$true)]
        [string]$version,
        [Parameter(Mandatory=$true)]
        [string]$branch,
        [Parameter(Mandatory=$true)]
        [string]$commitHash,
        [Parameter(Mandatory=$true)]
        [string]$timestamp

    )
    Process {
        $newVersion = 'public static readonly string Version = "' + $version + '";'
        $newBranch = 'public static readonly string Branch = "' + $branch + '";'
        $newCommitHash = 'public static readonly string Hashtag = "' + $commitHash + '";'
        $newTimestamp = 'public static readonly string Timestamp = "' + $timestamp + '";'

        $versionPattern = 'public static readonly string Version = ".*";'
        $branchPattern = 'public static readonly string Branch = ".*";'
        $commitHashPattern = 'public static readonly string Hashtag = ".*";'
        $timestampPattern = 'public static readonly string Timestamp = ".*";'
        
        $edited = (Get-Content $versionInfoFilePath) | ForEach-Object {
            % {$_ -replace "\/\*+.*\*+\/", "" } |
            % {$_ -replace "\/\/+.*$", "" } |
            % {$_ -replace "\/\*+.*$", "" } |
            % {$_ -replace "^.*\*+\/\b*$", "" } |
            % {$_ -replace $versionPattern, $newVersion} |
            % {$_ -replace $branchPattern, $newBranch} |
            % {$_ -replace $commitHashPattern, $newCommitHash } |
            % {$_ -replace $timestampPattern, $newTimestamp}
        }

        Set-Content -Path $versionInfoFilePath -Value $edited
    }
}
