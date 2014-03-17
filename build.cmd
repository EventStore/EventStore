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
echo build.cmd ^quick^|full^|clean-all^|v8^|js1^ ^[-Configuration=debug^|release^] ^[-Platform=x64^|x86^] ^[-Version=0.0.0.0^] ^[-SpecificVisualStudioVersion=Windows7.1SDK^|2013^|2012^|2010^] ^[-ForceNetwork^] ^[-Defines=^]