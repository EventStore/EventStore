[CmdletBinding()]
Param(
    [Parameter(HelpMessage="S3 Bucket", Mandatory=$true)]
    [string]$S3Bucket,
    [Parameter(HelpMessage="Semantic Version", Mandatory=$true)]
    [string]$SemanticVersion,
    [Parameter(HelpMessage="Build ID", Mandatory=$true)]
    [string]$BuildId,
    [Parameter(HelpMessage="Build Directory", Mandatory=$true)]
    [string]$BuildDirectory
)

Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
Install-Module -Name AWSPowerShell -Scope CurrentUser

Set-AWSCredential -AccessKey Env:AWS_ACCESS_KEY_ID -SecretKey Env:AWS_SECRET_ACCESS_KEY -StoreAs default

$basePath = Join-Path (Join-Path $SemanticVersion $BuildId) $BuildDirectory

Push-Location "packages"

Get-ChildItem -File -Recurse -Path "." | Foreach-Object {
    $key = (Join-Path $basePath (Resolve-Path $_.FullName -Relative).Substring(2))
    Write-Host "Uploading $_.Name to s3://$S3Bucket/$key"
    Write-S3Object -BucketName $S3Bucket -File $_.FullName -Key $key -CannedACLName NoACL
}

Pop-Location