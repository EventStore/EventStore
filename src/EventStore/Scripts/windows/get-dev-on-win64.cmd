@echo off
mode con: cols=170 lines=3000
set EventStoreDest=c:\EventStore

@powershell -NoProfile -ExecutionPolicy unrestricted -Command "iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))"  || goto :Error
set path=%systemdrive%\chocolatey\bin;%PATH%; || goto :Error
call cinst git || goto :Error
call cinst svn || goto :Error
call cinst VisualStudio2012WDX || goto :Error

set path=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\;%PATH%;%ProgramFiles(x86)%\Git\cmd;%ProgramFiles(x86)%\Subversion\bin; || goto :error


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
msbuild EventStore.sln /p:Configuration=Release || goto :Error
popd || goto :Error

pushd bin\eventstore\release\anycpu || goto :Error
md c:\EventStore\Data
start EventStore.SingleNode.exe --db=c:\EventStore\Data\db1
popd || goto :Error

popd || goto :Error

start http://127.0.0.1:2113/

exit /b 0
:Error
echo Something went wrong. See previous messages.
pause
exit /b 1