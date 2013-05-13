@echo off
pushd %~dp0 || goto :error
cd ..\..\v8 || goto :error

call %~dp0configure-cpp.cmd || goto :error
call :generate-project-files || goto :error
call :build-solution || goto :error
call :copy-files || goto :error

popd || goto :error

goto :EOF

:error
echo FAILED. See previous messages
exit /b 1


:generate-project-files
    call git clean -fx -- build || goto :error
    call git clean -dfx -- src || goto :error
    call git clean -dfx -- test || goto :error
    call git clean -dfx -- tools || goto :error
    call git clean -dfx -- preparser || goto :error
    if exist build\release del /f/s/q build\release || goto :error
    if exist build\debug del /f/s/q build\debug || goto :error
    python build\gyp_v8 -Dtarget_arch=x64 || goto :error
: -Dv8_use_snapshot='false' 
:    sed -iold -e 's/Debug/Release/g' build\all.vcxproj || goto :error
    sed -iold -e 's/Win32/x64/g' build\all.vcxproj || goto :error
:    sed -iold -e 's/ProgramDatabase//g' build\all.vcxproj || goto :error
  sed -iold -e 's/ProgramDatabase/OldStyle/g' tools\gyp\v8_base.vcxproj || goto :error
  sed -iold -e 's/ProgramDatabase/OldStyle/g' tools\gyp\v8_nosnapshot.vcxproj || goto :error
::    for %%t in (tools\gyp\*.vcxproj) do sed -iold -e 's/ProgramDatabase//g' %%t || goto :error
exit /b 0

:build-solution

    pushd build || goto :error
    msbuild all.sln /p:Configuration=Debug /p:Platform=x64 || goto :error
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