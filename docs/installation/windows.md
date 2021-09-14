# Windows

The prerequisites for installing on Windows are:

- NET Framework 4.0+

## Install from Chocolatey

EventStoreDB has [Chocolatey packages](https://chocolatey.org/packages/eventstore-oss) available that you can install with the following command in an elevated terminal:

```powershell
choco install eventstore-oss
```

## Download the binaries

You can also [download](https://eventstore.com/downloads/) a binary, unzip the archive and run from the folder location with an administrator console.

The following command starts EventStoreDB with the database stored at the path _./db_ and the logs in _./logs_. Read mode about configuring the EventStoreDB server node in the [Configuration section](./configuration.md).

```powershell
EventStore.ClusterNode.exe --db ./db --log ./logs
```

EventStoreDB runs in an administration context because it starts an HTTP server through `http.sys`. For permanent or production instances you need to provide an ACL such as:

```powershell
netsh http add urlacl url=http://+:2113/ user=DOMAIN\username
```

For more information, refer to Microsoft `add urlacl` [documentation](https://docs.microsoft.com/en-us/windows/win32/http/add-urlacl).

To build EventStoreDB from source, refer to the EventStoreDB [GitHub repository](https://github.com/EventStore/EventStore#windows).

## Uninstall

If you installed EventStoreDB with Chocolatey, you can uninstall with:

```powershell
choco uninstall eventstore-oss
```

This removes the `eventstore-oss` Chocolatey package.

If you installed EventStoreDB by [downloading a binary](https://eventstore.com/downloads/), you can remove it by:

* Deleting the `EventStore-OSS-Win-*` directory.
* Removing the directory from your PATH.
