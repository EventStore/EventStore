## EventStore Chocolatey Package

This project contains the scripts and nuspec used to build a chocolatey package for EventStore.

## Using these scripts

1. Use `.\updateVersion.ps1 -version <version number>` to set the version you want to package.
2. Run `.\build.ps1` to create a nupkg.
3. Run `cinst eventstore -Source .` to install the package locally.

## The chocolatey package

The chocolatey package downloads Event Store and unzips it to the chocolatey install location. 
Type `EventStore.ClusterNode.exe` to run the Event Store.
