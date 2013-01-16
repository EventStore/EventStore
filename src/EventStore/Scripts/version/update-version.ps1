param (
	[string]$version = "DEVELOPMENT VERSION"
)

$ErrorActionPreference = "Stop"

$curdir = Split-Path -parent $MyInvocation.MyCommand.Definition

Set-Location $curdir

$branch = git rev-parse --abbrev-ref HEAD
if ($LastExitCode -ne 0) {
  throw "Failed to get branch, exited with $LastExitCode"
}

$hashtag = git rev-parse HEAD
if ($LastExitCode -ne 0) {
  throw "Failed to get hashtag, exited with $LastExitCode"
}

Set-Location ../../EventStore.Common/Version

Copy-Item -Force Version.template Version.cs

$file = Get-Content "Version.cs"
$file = $file -replace "<VERSION>", $version
$file = $file -replace "<BRANCH>", $branch
$file = $file -replace "<HASHTAG>", $hashtag

Set-Content "Version.cs" $file

echo Done.
