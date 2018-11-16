@echo off

if '%1'=='/?' goto help
if '%1'=='/help' goto help
if '%1'=='--help' goto help
if '%1'=='-help' goto help
if '%1'=='-h' goto help

powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\build.ps1' %*;"
exit /B %errorlevel%

:help

echo Usage:
echo build.cmd ^[-Version=0.0.0.0^] ^[-Configuration=Debug^|Release^] ^[-BuildUI=yes^|no^]
echo.
echo Prerequisites:
echo Building EventStore database requires .NET Core SDK 2.1.402+ and .NET Framework 4.7.1+ (Developer Pack)
echo Building the UI requires Node.js (v8.11.4+)