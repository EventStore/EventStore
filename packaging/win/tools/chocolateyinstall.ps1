$ErrorActionPreference = 'Stop';

$packageName= 'eventstore-oss'

$encodedVersion=[uri]::EscapeDataString($env:ChocolateyPackageVersion)

$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://s3-eu-west-1.amazonaws.com/eventstore-binaries/binaries/$encodedVersion/windows/EventStore-OSS-Win-v$encodedVersion.zip"

$packageArgs = @{
  packageName   = $packageName
  unzipLocation = $toolsDir
  url           = $url

  silentArgs    = "/qn /norestart /l*v `"$env:TEMP\chocolatey\$($packageName)\$($packageName).MsiInstall.log`""
  validExitCodes= @(0, 3010, 1641)
  
  softwareName  = 'EventStore'
}

Install-ChocolateyZipPackage @packageArgs

Write-Output "Output to $toolsDir"