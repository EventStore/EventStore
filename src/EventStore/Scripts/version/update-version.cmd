@echo off   

if not exist %1\..\EventStore.Common\Properties\ESVersion.txt goto VERSION_NOT_FOUND

set /p _es_version=<%1\..\EventStore.Common\Properties\ESVersion.txt
for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do @set _es_branch=%%a
for /f "delims=" %%a in ('call git log -n1 --pretty^=format:"%%H@%%aD" HEAD') do @set _es_log=%%a

echo [assembly: System.Reflection.AssemblyVersion("%_es_version: =%.0")] > %TEMP%\es-ver.tmp
echo [assembly: System.Reflection.AssemblyFileVersion("%_es_version: =%.0")] >> %TEMP%\es-ver.tmp
echo [assembly: System.Reflection.AssemblyInformationalVersion("%_es_version: =%.%_es_branch: =%@%_es_log%")] >> %TEMP%\es-ver.tmp

fc %TEMP%\es-ver.tmp %1\..\EventStore.Common\AssemblyVersion.cs > nul && goto IDENTICAL
copy /y %TEMP%\es-ver.tmp %1\..\EventStore.Common\Properties\AssemblyVersion.cs
:IDENTICAL
del %TEMP%\es-ver.tmp 
exit /b 0
:VERSION_NOT_FOUND
echo No ESVersion.txt file found with current version!
exit /b 1
    