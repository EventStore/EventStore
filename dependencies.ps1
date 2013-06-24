# Dependencies
Properties {
    $baseDirectory = Resolve-Path .

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

Task Get-Dependencies {
    Get-GitRepoAtCommitOrRef -Verbose "V8" $v8Repository $v8Directory $v8Tag
    Get-SvnRepoAtRevision -Verbose "Python" $pythonRepository $pythonDirectory $pythonRevision
    Get-SvnRepoAtRevision -Verbose "GYP"  $gypRepository $gypDirectory $gypRevision
    Get-SvnRepoAtRevision -Verbose "CygWin" $cygwinRepository $cygwinDirectory $cygwinRevision
}

# Helper Functions - some of these rely on psake-provided constructs such as Exec { }.

Function Test-ShouldTryNetworkAccess {
    #Opaque GUID from - http://blogs.microsoft.co.il/blogs/scriptfanatic/archive/2010/03/09/quicktip-how-do-you-check-internet-connectivity.aspx
    $hasNetwork = [Activator]::CreateInstance([Type]::GetTypeFromCLSID([Guid]'{DCB00C01-570F-4A9B-8D69-199FDBA5723B}')).IsConnectedToInternet
    return ($hasNetwork -eq $true) -or ($forceNetwork -eq $true)
}

Function Test-GitRefOrCommitExists {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$refOrCommit
    )
    Process {
        try {
            $output = Exec { git show --quiet -s --format=%H "$refOrCommit^{commit}" 2>$null }
            return $true
        } catch {
            return $false
        }
    }
}

Function Get-GitRepoAtCommitOrRef
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$dependencyName,
        [Parameter(Mandatory=$true)][string]$repositoryAddress,
        [Parameter(Mandatory=$true)][string]$localPath,
        [Parameter(Mandatory=$true)][string]$commitOrRef
    )
    Process {
        Write-Host "Getting dependency $dependencyName via Git"

        $localPathGitDirectory = Join-Path $localPath ".git/"
        Write-Verbose "Testing for existence of: $localPathGitDirectory"
        
        if ((Test-Path $localPathGitDirectory -PathType Container) -eq $true)
        {
            Write-Verbose "$localPathGitDirectory already exists"
            Push-Location -ErrorAction Stop -Path $localPath

            try {
                if ((Test-GitRefOrCommitExists $commitOrRef) -eq $true) {
                    Write-Verbose "$commitOrRef exists in repository - checking out"
                    Exec { git checkout --quiet $commitOrRef}
                } else {
                    Write-Verbose "$commitOrRef is not in repository"
                    if ((Test-ShouldTryNetworkAccess) -eq $true) {
                        Write-Verbose "Fetching from $repositoryAddress"
                        & { git fetch }
                    } else {
                        throw "$commitOrRef does not in the repository, and network connectivity is not detected. Pass forceNetwork = $true to try anyway."
                    }
                    Write-Verbose "Checking out $commitOrRef"
                    try {
                        & { git checkout --quiet $commitOrRef}
                    } catch {
                        throw "Cannot check out $commitOrRef - it does not exist not in the repository"
                    }
                }
            } finally {
                Pop-Location -ErrorAction Stop
            }
        } else {
            Write-Verbose "$localPathGitDirectory not found"

            if ((Test-ShouldTryNetworkAccess) -eq $true) {
                Write-Verbose "Cloning git repository from $repositoryAddress to $localPath"

                Exec { git clone --quiet $repositoryAddress $localPath }
            
                if ($commitOrRef -ne "") {
                    Push-Location -ErrorAction Stop -Path $localPath
                    try {
                        Write-Verbose "Checking out $commitOrRef"
                        Exec { git checkout --quiet $commitOrRef}
                    } catch {
                        Assert ($false) "Cannot check out $commitOrRef - it is not in the repository"
                    } finally {
                        Pop-Location -ErrorAction Stop
                    }
                }
            } else {
                throw "Repository $localPathGitDirectory does not exist, and network connectivity is not detected. Pass forceNetwork = $true to try anyway."
            }


        }
    }
}

Function Test-SvnRepoIsAtRevision {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)][string]$revision
    )
    Process {
        try {
            [xml]$svnInfo = Exec { svn info --xml }
            $actualRevision = $svnInfo.info.entry.commit.GetAttribute("revision")
            return ($actualRevision -eq $revision)
        } catch {
            return $false
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
        [Parameter(Mandatory=$true)][string]$revision
    )
    Process {
        Write-Host "Getting dependency $dependencyName via Subversion"
        
        $revisionString = "-r$revision"
        
        $localPathSvnDirectory = Join-Path $localPath ".svn/"
        Write-Verbose "Testing for existence of: $localPathSvnDirectory"
        if ((Test-Path $localPathSvnDirectory -PathType Container) -eq $true)
        {
            Write-Verbose "$localPathSvnDirectory already exists"
            
            Push-Location -ErrorAction Stop -Path $localPath
            try {
                if ((Test-SvnRepoIsAtRevision $revision) -eq $false) {
                    Write-Verbose "Updating to revision $revision"
                    if ((Test-ShouldTryNetworkAccess) -eq $true) {
                        Exec { svn update --quiet $revisionString }
                    } else {
                        Assert ($false) "SVN Repository can't be updated, as there is no network access. To try anyway, pass value '$true' as parameter 'forceNetwork'"
                    }
                } else {
                    Write-Verbose "Already at revision $revision."
                }
            } finally {
                Pop-Location -ErrorAction Stop
            }
        } else {
            Write-Verbose "$localPathSvnDirectory not found"
            Write-Verbose "Checking out svn repository from $repositoryAddress (revision $revision) to $localPath"

            if (((Test-SvnRepoIsAtRevision) -eq $false) -and ((Test-ShouldTryNetworkAccess) -eq $true)) {
                Exec { svn checkout --quiet $revisionString $repositoryAddress $localPath }
            } else {
                Assert ($false) "SVN Repository can't be checked out, as there is no network access. To try anyway, pass value '$true' as parameter 'forceNetwork'"
            }
        }
    }
}