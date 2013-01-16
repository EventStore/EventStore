param (
)

$ErrorActionPreference = "Stop"

$curdir = Split-Path -parent $MyInvocation.MyCommand.Definition

Set-Location $curdir

Set-Location ../../EventStore.Common/Version

Copy-Item -Force Version.template Version.cs

echo Done.
