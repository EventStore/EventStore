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
..\..\..\..\tools\ilmerge\ILMerge.exe /xmldocs /internalize /targetplatform:v4,"%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /out:..\..\..\eventstore.client\EventStore.ClientAPI.dll eventstore.clientapi.dll protobuf-net.dll Newtonsoft.Json.dll

exit /b 0
