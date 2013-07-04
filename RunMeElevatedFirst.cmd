@echo off

rem This script sets the execution policy of 32 bit Powershell to remote signed. It's likely
rem that if you're running 64 bit Windows, this won't have been done previously. However Visual
rem Studio is still a 32 bit app and therefore runs pre- and post-build event tasks using 32 bit
rem Powershell. The pre- and post-build tasks simply patch the AssemblyInfo.cs file in the
rem EventStore.Common project on each build with the current branch and commit hash such that you
rem can tell which version you're running during local testing.

if "%ProgramFiles(x86)%" == "hello" (
	call %SystemRoot%\system32\WindowsPowerShell\v1.0\powershell.exe -command "Set-ExecutionPolicy RemoteSigned"
) else (
	call %SystemRoot%\syswow64\WindowsPowerShell\v1.0\powershell.exe% -command "Set-ExecutionPolicy RemoteSigned"
)