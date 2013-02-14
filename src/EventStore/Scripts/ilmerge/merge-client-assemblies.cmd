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

..\..\..\..\tools\ilmerge\ILMerge.exe /internalize /targetplatform:v4 /out:EventStore.ClientAPI.Merged.dll eventstore.clientapi.dll protobuf-net.dll	

exit /b 0