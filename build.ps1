[CmdletBinding()]
Param(
    [Parameter(HelpMessage="Assembly version number (x.y.z.0 where x.y.z is the semantic version)")]
    [string]$Version = "0.0.0.0",
    [Parameter(HelpMessage="Configuration (Debug, Release)")]
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Release",
    [Parameter(HelpMessage="Build UI (yes,no)")]
    [ValidateSet("yes","no")]
    [string]$BuildUI = "no",
    [Parameter(HelpMessage="Run Tests (yes,no)")]
    [ValidateSet("yes","no")]
    [string]$RunTests = "no"
)

Function Write-Info {
    Param([string]$message)
    Process {
        Write-Host $message -ForegroundColor Cyan
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

Function Start-Build{
    #Make compatible with Powershell 2
    if(!$PSScriptRoot) { $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent }

    #Configuration
    $platform = "x64"

    $baseDirectory = $PSScriptRoot
    $srcDirectory = Join-Path $baseDirectory "src"
    $binDirectory = Join-Path $baseDirectory "bin"
    $libsDirectory = Join-Path $srcDirectory "libs"
    $eventStoreSolution = Join-Path $srcDirectory "EventStore.sln"

    $uiSrcDirectory = Join-Path $srcDirectory "EventStore.UI\"
    $uiDistDirectory = Join-Path $srcDirectory "EventStore.ClusterNode.Web\clusternode-web\"

    Write-Info "Build Configuration"
    Write-Info "-------------------"

    Write-Info "Version: $Version"
    Write-Info "Platform: $platform"
    Write-Info "Configuration: $Configuration"    
    Write-Info "Build UI: $BuildUI"
    Write-Info "Run Tests: $RunTests"

    #Build Event Store UI
    if ($BuildUI -eq "yes") {
        #Build the UI    
        if (Test-Path $uiDistDirectory) {
            Remove-Item -Recurse -Force $uiDistDirectory
        }
        Push-Location $uiSrcDirectory
            if(-Not (Test-Path (Join-Path $uiSrcDirectory "package.json"))) {
                Exec { git submodule update --init ./ }
            }
            Exec { npm install bower@~1.8.4 -g }
            Exec { bower install --allow-root }
            Exec { npm install gulp@~3.8.8 -g }
            Exec { npm install }
            Exec { gulp dist }        
            Exec { mv es-dist $uiDistDirectory }
        Pop-Location
    }

    #Build Event Store (Patch AssemblyInfo, Build, Revert AssemblyInfo)
    Remove-Item -Force -Recurse $binDirectory -ErrorAction SilentlyContinue > $null

    $commitHash = Get-GitCommitHash
    $timestamp = Get-GitTimestamp
    $branchName = Get-GitBranchOrTag
    
    $versionInfoFile = Resolve-Path (Join-Path $srcDirectory (Join-Path "EventStore.Common" (Join-Path "Utils" "VersionInfo.cs"))) -Relative
    try {
        Write-Info "Patching $versionInfoFile with product information."
        Patch-VersionInfo -versionInfoFilePath $versionInfoFile -version $Version -commitHash $commitHash -timestamp $timestamp -branch $branchName

        Exec { dotnet build -c $configuration /p:Version=$Version $eventStoreSolution }
    } finally {
        Write-Info "Reverting $versionInfoFile to original state."
        & { git checkout --quiet $versionInfoFile }
    }
    if($RunTests -eq "yes"){
        (Get-ChildItem -Attributes Directory src | % FullName) -Match '.Tests' | `
        ForEach-Object {
          dotnet test -v normal -c $Configuration --no-build --logger trx --results-directory testResults $_ -- RunConfiguration.TargetPlatform=x64
          if (-Not $?) { throw "Exit code is $?" }
        }
    }
}

Start-Build