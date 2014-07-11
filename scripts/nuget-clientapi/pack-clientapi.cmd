@echo off
pushd %~dp0
IF [%1]==[] (
	echo "No version specified - use pack-clientapi.cmd <version>"
	exit /b1
)
..\..\tools\nuget\nuget.exe pack -symbols -version %1 EventStore.Client.nuspec
