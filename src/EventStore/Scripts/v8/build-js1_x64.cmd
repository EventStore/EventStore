@echo off
call %~dp0configure-cpp.cmd || goto :error
msbuild ..\..\EventStore.Projections.v8Integration\EventStore.Projections.v8Integration.vcxproj /p:Configuration=Debug /p:Platform=x64 /p:PlatformToolset=%platformtoolset% /t:rebuild || goto :error

goto :EOF

:error
echo FAILED. See previous messages
exit /b 1
