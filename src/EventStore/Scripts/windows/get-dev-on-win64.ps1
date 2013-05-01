Set-Content $env:temp\get-event-store.cmd ((new-object net.webclient).DownloadString('https://github.com/EventStore/EventStore/raw/dev/src/EventStore/Scripts/windows/get-dev-on-win64.cmd'))
