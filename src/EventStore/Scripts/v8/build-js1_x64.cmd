@echo off

call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\bin\amd64\vcvars64.bat"  || goto :error
msbuild ..\..\EventStore.Projections.v8Integration\EventStore.Projections.v8Integration.vcxproj /p:Configuration=Debug /p:Platform=x64 /t:rebuild || goto :error

goto :EOF

:error
echo FAILED. See previous messages
exit /b 1
