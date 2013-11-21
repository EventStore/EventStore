@echo off

if '%1'=='/?' goto help
if '%1'=='/help' goto help
if '%1'=='--help' goto help
if '%1'=='-help' goto help
if '%1'=='-h' goto help

:: if ($psake.build_success -eq $false) { exit 1 } else { exit 0 }"

powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\tools\powershell\build.ps1' %*;"
exit /B %errorlevel%

:help

echo Usage:
echo build.cmd build-type configuration platform version specific-vs-version force-network defines
