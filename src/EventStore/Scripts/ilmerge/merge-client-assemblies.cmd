@echo on
pushd %~dp0 || goto :error
cd ..\..\..\..\bin\eventstore\release\anycpu || goto :error

call :merge-assemblies || goto :error

popd || goto :error

goto :EOF

:error
echo FAILED. See previous messages
exit /b 1

:merge-assemblies

mkdir ..\..\..\eventstore.client\
..\..\..\..\tools\ilmerge\ILMerge.exe /internalize /targetplatform:v4 /out:..\..\..\eventstore.client\EventStore.ClientAPI.dll eventstore.clientapi.dll protobuf-net.dll

exit /b 0