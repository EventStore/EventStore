param (
    [string]$version = $(throw "Version is required.")
)


$outDir = Join-Path $PSScriptRoot '..\..\packages'
if ((Test-Path $outDir) -eq $false) {
    New-Item -Path $outDir -ItemType Directory > $null
}

Push-Location
choco pack --version $version (Join-Path $PSScriptRoot 'src\eventstore-oss.nuspec') --outdir $outDir
Pop-Location