rem @echo off   

set MSBuildProjectDirectory = %1

echo %MSBuildProjectDirectory%

if not exist %1\Properties\ESVersion.txt goto VERSION_NOT_FOUND
if not exist %1\Properties\AssemblyVersion.cs goto ASSEMBLY_VERSION_NOT_FOUND

set /p _es_version=<%1\Properties\ESVersion.txt || goto ERROR

set _es_branch = ""
for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do @set _es_branch=%%a || goto ERROR

set _es_hashtag = ""
for /f "delims=" %%a in ('git rev-parse HEAD') do @set _es_hashtag=%%a || goto ERROR

git log HEAD -s -n1 --pretty="%%aD" > %TEMP%\es-timestamp.tmp
set /p _es_timestamp=< %TEMP%\es-timestamp.tmp || goto ERROR

if "%_es_branch: =%" == "" goto ERROR
if "%_es_hashtag: =%" == "" goto ERROR

echo [assembly: System.Reflection.AssemblyVersion("%_es_version: =%.0")] > %TEMP%\es-ver.tmp
echo [assembly: System.Reflection.AssemblyFileVersion("%_es_version: =%.0")] >> %TEMP%\es-ver.tmp
echo [assembly: System.Reflection.AssemblyInformationalVersion("%_es_version: =%.%_es_branch: =%@%_es_hashtag: =%@%_es_timestamp%")] >> %TEMP%\es-ver.tmp

fc %TEMP%\es-ver.tmp %1\Properties\AssemblyVersion.cs > nul && goto IDENTICAL

copy /y %TEMP%\es-ver.tmp %1\Properties\AssemblyVersion.cs

:IDENTICAL

del %TEMP%\es-ver.tmp 

exit /b 0

:VERSION_NOT_FOUND
echo No ESVersion.txt file found with current version!
exit /b 1

:ASSEMBLY_VERSION_NOT_FOUND
echo No AssemblyVersion.cs file found in EventStore.Common!
exit /b 1

:ERROR
echo Unknown error happened!
exit /b 1
    