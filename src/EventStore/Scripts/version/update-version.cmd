@echo on   
pushd %~dp0

if not exist "..\..\EventStore.Common\Properties\ESVersion.txt" goto ES_VERSION_NOT_FOUND
if not exist "..\..\EventStore.ClientAPI\Properties\ESCAVersion.txt" goto ESCA_VERSION_NOT_FOUND

set _es_tmpfile="%TEMP%\es-ver-%RANDOM%-%TIME:~6,5%.tmp"
set _esca_tmpfile="%TEMP%\esca-ver-%RANDOM%-%TIME:~6,5%.tmp"

set /p _es_version=<"..\..\EventStore.Common\Properties\ESVersion.txt"
set /p _esca_version=<"..\..\EventStore.ClientAPI\Properties\ESCAVersion.txt"
for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do @set _es_branch=%%a
for /f "delims=" %%a in ('call git log -n1 --pretty^=format:"%%H@%%aD" HEAD') do @set _es_log=%%a

echo [assembly: System.Reflection.AssemblyVersion("%_es_version: =%.0")] > %_es_tmpfile%
echo [assembly: System.Reflection.AssemblyFileVersion("%_es_version: =%.0")] >> %_es_tmpfile%
echo [assembly: System.Reflection.AssemblyInformationalVersion("%_es_version: =%.%_es_branch: =%@%_es_log%")] >> %_es_tmpfile%

echo [assembly: System.Reflection.AssemblyVersion("%_esca_version: =%.0")] > %_esca_tmpfile%
echo [assembly: System.Reflection.AssemblyFileVersion("%_esca_version: =%.0")] >> %_esca_tmpfile%
echo [assembly: System.Reflection.AssemblyInformationalVersion("%_esca_version: =%.%_es_branch: =%@%_es_log%")] >> %_esca_tmpfile%

fc %_es_tmpfile% ..\..\EventStore.Common\Properties\AssemblyVersion.cs && goto IDENTICAL_ES
copy /y %_es_tmpfile% "..\..\EventStore.Common\Properties\AssemblyVersion.cs"
:IDENTICAL_ES

fc %_esca_tmpfile% ..\..\EventStore.ClientAPI\Properties\AssemblyVersion.cs && goto IDENTICAL_ESCA
copy /y %_esca_tmpfile% "..\..\EventStore.ClientAPI\Properties\AssemblyVersion.cs"
:IDENTICAL_ESCA

del %_es_tmpfile%
del %_esca_tmpfile%
exit /b 0

:ES_VERSION_NOT_FOUND
echo No ESVersion.txt file found with current version!
exit /b 1

:ESCA_VERSION_NOT_FOUND
echo No ESCAVersion.txt file found with current version!
exit /b 1
    
