@echo off
pushd %~dp0 || goto :error
cd ..\..\v8

call :setup-environment
call :generate-project-files
call :build-solution
call :copy-files

popd || goto :error

goto :EOF

:error
echo FAILED. See previous messages
exit /b 1


:setup-environment

    path %PATH%;%~dp0..\..\v8\third_party\python_26\;C:\Windows\Microsoft.NET\Framework64\v4.0.30319\; || goto :error
    call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\bin\amd64\vcvars64.bat"  || goto :error

exit /b 0


:generate-project-files
    git clean -fx -- build || goto :error
    git clean -dfx -- src || goto :error
    git clean -dfx -- test || goto :error
    git clean -dfx -- tools || goto :error
    git clean -dfx -- preparser || goto :error
    if exist build\release del /f/s/q build\release || goto :error
    if exist build\debug del /f/s/q build\debug || goto :error
    python build\gyp_v8 -Dtarget_arch=x64 || goto :error
:    sed -iold -e 's/Debug/Release/g' build\all.vcxproj || goto :error
    sed -iold -e 's/Win32/x64/g' build\all.vcxproj || goto :error
    sed -iold -e 's/ProgramDatabase//g' build\all.vcxproj || goto :error
    for %%t in (tools\gyp\*.vcxproj) do sed -iold -e 's/ProgramDatabase//g' %%t || goto :error
exit /b 0

:build-solution

    pushd build || goto :error
    msbuild all.sln /p:Configuration=Debug /p:Platform=x64
    popd || goto :error

exit /b 0

:copy-files
	
    pushd build\Debug\lib  || goto :error
    mkdir ..\..\..\..\Libs\x64
    copy *.lib ..\..\..\..\Libs\x64 || goto: error
    popd || goto :error

    pushd include || goto :error
    mkdir ..\..\Libs\include
    copy *.h ..\..\Libs\include || goto: error
    popd || goto :error

exit /b 0