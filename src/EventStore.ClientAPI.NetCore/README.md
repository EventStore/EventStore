Migration based on code from `EventStore\ClientAPI.NetCore` repo
at commit 6162cf3ab0cd0fb5f0331caf6cf77b43f7fcc518

Repo aliases:

`main` = `EventStore/EventStore`
`netcore` = `EventStore/ClientAPI.NetCore`

# Differences

- `Common/Log/DebugLogger.cs`: in main was `System.Diagnostics.Trace`, in netcore was `Debug`. Used `main`
- `Common/Log/FileLogger.cs` used `main` with Dispose fixes and ctor cleanup
- `Common/Utils/Threading/TaskExtensions.cs` just newlines changes
- `Exceptions/*Exception.cs` doesnt expose Serialization constructor. hidden by compiler define EVENTSTORE_CLIENT_NO_EXCEPTION_SERIALIZATION
- use assembly metadata defined in `EventStore.ClientAPI.NetCore/EventStore.ClientAPI.csproj`
- `Internal/Consts.cs` new constant in main
- `Internal/EventStoreNodeConnection.cs`:
    - in main the async keyword is used, in netcore Task is used directly. using main 
    - removed unused namespace `System.Data.SqlClient`
