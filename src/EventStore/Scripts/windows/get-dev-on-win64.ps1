Set-Content $env:temp\get-event-store.cmd ((new-object net.webclient).DownloadString(''))
