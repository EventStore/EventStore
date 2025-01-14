$ErrorActionPreference = 'Stop';

$packageName= 'eventstore-oss'

$encodedVersion=[uri]::EscapeDataString($env:ChocolateyPackageVersion)

$packageName= 'eventstore-oss'
$zipFileName= "EventStore-OSS-Win-v$encodedVersion.zip"

Uninstall-ChocolateyZipPackage -PackageName $packageName -ZipFileName $zipFileName