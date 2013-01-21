@echo off   

if not exist "%1\..\EventStore.Common\Properties\ESVersion.txt" goto VERSION_NOT_FOUND

set _es_tmpfile="%TEMP%\es-ver-%RANDOM%-%TIME:~6,5%.tmp"

set /p _es_version=<"%1\..\EventStore.Common\Properties\ESVersion.txt"
for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do @set _es_branch=%%a
for /f "delims=" %%a in ('call git log -n1 --pretty^=format:"%%H@%%aD" HEAD') do @set _es_log=%%a

echo [assembly: System.Reflection.AssemblyVersion("%_es_version: =%.0")] > %_es_tmpfile%
echo [assembly: System.Reflection.AssemblyFileVersion("%_es_version: =%.0")] >> %_es_tmpfile%
echo [assembly: System.Reflection.AssemblyInformationalVersion("%_es_version: =%.%_es_branch: =%@%_es_log%")] >> %_es_tmpfile%

fc %_es_tmpfile% "%1\..\EventStore.Common\Properties\AssemblyVersion.cs" && goto IDENTICAL
copy /y %_es_tmpfile% "%1\..\EventStore.Common\Properties\AssemblyVersion.cs"
:IDENTICAL
del %_es_tmpfile%
exit /b 0
:VERSION_NOT_FOUND
echo No ESVersion.txt file found with current version!
exit /b 1
    