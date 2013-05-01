@echo off

set EventStoreDest=c:\EventStore

@powershell -NoProfile -ExecutionPolicy unrestricted -Command "iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))" && path %PATH%;%systemdrive%\chocolatey\bin || goto :Error
call cinst git || goto :Error
call cinst svn || goto :Error
call cinst VisualStudio2012WDX || goto :Error

path %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\;%PATH%;%ProgramFiles(x86)%\Git\cmd;%ProgramFiles(x86)%\Subversion\bin; || goto :error


git clone git://github.com/EventStore/EventStore.git %EventStoreDest% || goto :Error
pushd %EventStoreDest%  || goto :Error
git checkout dev || goto :Error

pushd src\EventStore\Scripts\v8 || goto :Error
call get-v8.cmd || goto :Error
popd || goto :Error

pushd src\EventStore\Scripts\v8 || goto :Error
call build-v8_release_x64.cmd  || goto :Error
popd || goto :Error

pushd src\EventStore\Scripts\v8 || goto :Error
call build-js1_x64.cmd   || goto :Error
popd || goto :Error

pushd src\EventStore || goto :Error
msbuild src\EventStore\EventStore.sln || goto :Error
popd || goto :Error

popd || goto :Error

exit /b 0
:Error
echo Something went wrong. See previous messages.
pause
exit /b 1