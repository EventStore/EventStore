# Event Store Build (.NET/Windows) - eventstore.ps1
# Use Invoke-psake ? to see further description

Framework "4.0x64"

Task default -depends ?

Task ? -description "Writes script documentation to the host" {
    Write-Host "Builds the managed part of the Event Store"
}

# Directories
Properties {
    $baseDirectory = Resolve-Path .
    $srcDirectory = Join-Path $baseDirectory (Join-Path "src" "EventStore")
    $libsDirectory = Join-Path $srcDirectory "libs"
    $outputDirectory = Join-Path $baseDirectory "bin"
}

# Project Files
Properties {
    $eventStoreSolution = Join-Path $srcDirectory "EventStore.sln"
}



Task Clean-EventStore {
    Remove-Item -Recurse -Force $outputDirectory -ErrorAction SilentlyContinue
}

Task Clean-Libs {
    Push-Location $libsDirectory
    Exec { git clean --quiet -xdf }
    Pop-Location
}

Task Build-EventStore {
    Exec { msbuild $eventStoreSolution /p:Configuration=$configuration /p:Platform="Any CPU" /p:OutDir=$outputDirectory }
}