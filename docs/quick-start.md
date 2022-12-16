---
title: "Quick Start"
---

# Quick Start

This is the simplest way to start EventStoreDB and connect a client to it.
In this tutorial, we're going to make use of dev mode, which is available in versions 22.10.0 and beyond.

For older versions, please check the Installation docs.

## Installing and running the server

:::: code-group
::: code-group-item Windows

<br />

You can install EventStoreDB using [Chocolatey]():

```powershell
chocolatey install eventstore-oss
```

And run it with the command:

```powershell
EventStore.ClusterNode.exe --dev
```

If you don't want to use chocolatey, you can also download the binaries from the [Downloads page](https://www.eventstore.com/downloads) and run EventStore directly from them.

This command will do two things:

1. Generate and trust a dev certificate for your EventStoreDB node
2. Start EventStoreDB on localhost with some good defaults for a development instance

You may get some pop-ups to provide admin permission to install the dev certificates.

After this, you should get some startup logs in the terminal:

```
[ 8448, 1,12:56:16.252,INF]
"ES VERSION:"             "22.10.0.0" ("tags/oss-v22.10.0"/"242b35056", "Thu, 10 Nov 2022 13:45:56 +0000")
[ 8448, 1,12:56:16.257,INF] "OS ARCHITECTURE:"        X64
[ 8448, 1,12:56:16.258,INF] "OS:"                     Windows ("Microsoft Windows NT 10.0.22000.0")
[ 8448, 1,12:56:16.259,INF] "RUNTIME:"                ".NET 6.0.11/943474ca1" (64-bit)
[ 8448, 1,12:56:16.260,INF] "GC:"                     "3 GENERATIONS" "IsServerGC: False" "Latency Mode: Interactive"
[ 8448, 1,12:56:16.260,INF] "LOGS:"                   "C:\\ProgramData\\chocolatey\\lib\\eventstore-oss\\tools\\EventStore-OSS-Windows-2019-v22.10.0\\logs"
[ 8448, 1,12:56:16.285,INF] MODIFIED OPTIONS:

     DEV:                                     true (Command Line)

DEFAULT OPTIONS:

... Default options truncated ...

[ 8448, 1,12:56:16.285,INF] Dev mode is enabled.
[ 8448, 1,12:56:16.285,WRN]
==============================================================================================================
DEV MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.
DEV MODE WILL GENERATE AND TRUST DEV CERTIFICATES FOR RUNNING A SINGLE SECURE NODE ON LOCALHOST.
==============================================================================================================

[ 8448, 1,12:56:16.351,WRN] Automatically trusting dev certs is only supported on Windows.
Please trust certificate "[Subject]\r\n  CN=localhost\r\n\r\n[Issuer]\r\n  CN=localhost\r\n\r\n[Serial Number]\r\n  00C90800CAFA426736\r\n\r\n[Not Before]\r\n  2022-11-17 4:49:52 PM\r\n\r\n[Not After]\r\n  2022-12-17 4:49:52 PM\r\n\r\n[Thumbprint]\r\n  24C4A93456EBDEC9818D2413CC8AB7DC91F74EFE\r\n" if it's not trusted already.
[ 8448, 1,12:56:16.352,INF] Running in dev mode using certificate '"[Subject]\r\n  CN=localhost\r\n\r\n[Issuer]\r\n  CN=localhost\r\n\r\n[Serial Number]\r\n  00C90800CAFA426736\r\n\r\n[Not Before]\r\n  2022-11-17 4:49:52 PM\r\n\r\n[Not After]\r\n  2022-12-17 4:49:52 PM\r\n\r\n[Thumbprint]\r\n  24C4A93456EBDEC9818D2413CC8AB7DC91F74EFE\r\n"'
[ 8448, 1,12:56:16.516,DBG] MessageHierarchy initialization took 00:00:00.1520756.

... Startup logs truncated ...

[ 8448,14,12:56:18.064,INF] ========== ["127.0.0.1:2113"] IS LEADER... SPARTA!
[ 8448,22,12:56:18.064,DBG] Persistent subscriptions Became Leader so now handling subscriptions
[ 8448,14,12:56:18.064,DBG] Searching for incomplete scavenges on node "127.0.0.1:2113".
[ 8448,14,12:56:18.068,DBG] Persistent Subscriptions have been started.
[ 8448,14,12:56:18.093,DBG] Max age and $ops read permission already set for the "$scavenges" stream.
[ 8448,14,12:56:18.099,DBG] No incomplete scavenges found on node "127.0.0.1:2113".
[ 8448,25,12:56:18.102,INF] PROJECTIONS SUBSYSTEM: Starting components for Instance: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,25,12:56:18.103,DBG] PROJECTIONS: Projection Core Coordinator component starting. Correlation: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,25,12:56:18.104,DBG] PROJECTIONS: Starting Projections Core Coordinator
[ 8448,25,12:56:18.105,DBG] PROJECTIONS: Starting Projections Manager. Correlation: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,25,12:56:18.106,DBG] PROJECTIONS: Reading Existing Projections from "$projections-$all"
[ 8448,25,12:56:18.107,DBG] PROJECTIONS SUBSYSTEM: Component '"ProjectionManager"' started for Instance: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,25,12:56:18.107,DBG] PROJECTIONS: SubComponent Started: "EventReaderCoreService"
[ 8448,25,12:56:18.107,DBG] PROJECTIONS: SubComponent Started: "ProjectionCoreService"
[ 8448,25,12:56:18.107,DBG] PROJECTIONS SUBSYSTEM: Component '"ProjectionCoreCoordinator"' started for Instance: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,25,12:56:18.107,INF] PROJECTIONS SUBSYSTEM: All components started for Instance: 270e034d-063e-4b55-a7db-42163feb6033
[ 8448,14,12:56:18.108,INF] ========== ["127.0.0.1:2113"] Sub System '"Projections"' initialized.
[ 8448,25,12:56:18.121,DBG] PROJECTIONS: Found the following projections in "$projections-$all": ["$streams", "$stream_by_category", "$by_category", "$by_event_type", "$by_correlation_id"]
[ 8448,25,12:56:18.124,DBG] Adding projection ff73ee61-f612-45cf-a6d7-79f1788c8360@"$streams" to list
[ 8448,25,12:56:18.125,DBG] Adding projection 05a2d4e7-9026-4cd3-a689-279d35362207@"$stream_by_category" to list
[ 8448,25,12:56:18.125,DBG] Adding projection cc9a3d38-26f8-4121-9549-2d0ec63ae259@"$by_category" to list
[ 8448,25,12:56:18.125,DBG] Adding projection 24cb82b7-0880-4c86-a77f-1078ba66fe57@"$by_event_type" to list
[ 8448,25,12:56:18.125,DBG] Adding projection 0f8fd68f-117a-4975-b9e4-96d5960f8178@"$by_correlation_id" to list
```

Your EventStoreDB node is now running.

:::
::: code-group-item Ubuntu

You can install EventStoreDB using [PackageCloud]():

```bash
curl -s https://packagecloud.io/install/repositories/EventStore/EventStore-OSS/script.deb.sh | sudo bash
sudo apt-get install eventstore-oss=22.10.0
```

And run it with the command:

```bash
sudo eventstored --dev
```

This command will do two things:

1. Generate a dev certificate for your EventStoreDB node
2. Start EventStoreDB on localhost with some good defaults for a development instance

You may see some warnings in the output about only being able to trust certificates on Windows machines. This is expected and can be ignored.

After this, you should see a startup log like the following in your terminal:

```
[ 2148, 1,17:02:32.431,INF]
"ES VERSION:"             "22.10.0.0" ("tags/oss-v22.10.0"/"242b35056", "Thu, 10 Nov 2022 13:45:56 +0000")
[ 2148, 1,17:02:32.483,INF] "OS ARCHITECTURE:"        X64
[ 2148, 1,17:02:32.503,INF] "OS:"                     Linux ("Unix 4.19.128.0")
[ 2148, 1,17:02:32.507,INF] "RUNTIME:"                ".NET 6.0.11/943474ca1" (64-bit)
[ 2148, 1,17:02:32.508,INF] "GC:"                     "3 GENERATIONS" "IsServerGC: False" "Latency Mode: Interactive"
[ 2148, 1,17:02:32.509,INF] "LOGS:"                   "/var/log/eventstore"
[ 2148, 1,17:02:32.551,INF] MODIFIED OPTIONS:

     CLUSTER SIZE:                            1 (Yaml)
     DEV:                                     true (Command Line)
     RUN PROJECTIONS:                         None (Yaml)

DEFAULT OPTIONS:

... Default options truncated...

[ 2148, 1,17:02:32.551,INF] Dev mode is enabled.
[ 2148, 1,17:02:32.551,WRN]
==============================================================================================================
DEV MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.
DEV MODE WILL GENERATE AND TRUST DEV CERTIFICATES FOR RUNNING A SINGLE SECURE NODE ON LOCALHOST.
==============================================================================================================

[ 2148, 1,17:02:32.631,ERR] An error occurred while listing the certificates: "System.Security.Cryptography.CryptographicException: Unix LocalMachine X509Store is limited to the Root and CertificateAuthority stores.\n ---> System.PlatformNotSupportedException: Unix LocalMachine X509Store is limited to the Root and CertificateAuthority stores.\n   --- End of inner exception stack trace ---\n   at Internal.Cryptography.Pal.StorePal.FromSystemStore(String storeName, StoreLocation storeLocation, OpenFlags openFlags)\n   at System.Security.Cryptography.X509Certificates.X509Store.Open(OpenFlags flags)\n   at EventStore.Common.DevCertificates.CertificateManager.ListCertificates(StoreName storeName, StoreLocation location, Boolean isValid, Boolean requireExportable) in /home/runner/work/TrainStation/TrainStation/build/oss-eventstore/src/EventStore.Common/DevCertificates/CertificateManager.cs:line 67"
[ 2148, 1,17:02:32.711,ERR] An error occurred while listing the certificates: "System.Security.Cryptography.CryptographicException: Unix LocalMachine X509Store is limited to the Root and CertificateAuthority stores.\n ---> System.PlatformNotSupportedException: Unix LocalMachine X509Store is limited to the Root and CertificateAuthority stores.\n   --- End of inner exception stack trace ---\n   at Internal.Cryptography.Pal.StorePal.FromSystemStore(String storeName, StoreLocation storeLocation, OpenFlags openFlags)\n   at System.Security.Cryptography.X509Certificates.X509Store.Open(OpenFlags flags)\n   at EventStore.Common.DevCertificates.CertificateManager.ListCertificates(StoreName storeName, StoreLocation location, Boolean isValid, Boolean requireExportable) in /home/runner/work/TrainStation/TrainStation/build/oss-eventstore/src/EventStore.Common/DevCertificates/CertificateManager.cs:line 67"
[ 2148, 1,17:02:32.712,WRN] Automatically trusting dev certs is only supported on Windows.
Please trust certificate "[Subject]\n  CN=localhost\n\n[Issuer]\n  CN=localhost\n\n[Serial Number]\n  00EB3B792F1143CF78\n\n[Not Before]\n  12/07/2022 17:02:32\n\n[Not After]\n  01/07/2023 17:02:32\n\n[Thumbprint]\n  6183FD84910768ABE2BFD299BC615FF6EA032F91\n" if it's not trusted already.
[ 2148, 1,17:02:32.713,INF] Running in dev mode using certificate '"[Subject]\n  CN=localhost\n\n[Issuer]\n  CN=localhost\n\n[Serial Number]\n  00EB3B792F1143CF78\n\n[Not Before]\n  12/07/2022 17:02:32\n\n[Not After]\n  01/07/2023 17:02:32\n\n[Thumbprint]\n  6183FD84910768ABE2BFD299BC615FF6EA032F91\n"'

... Startup logs truncated ...

[ 2148,14,17:02:34.091,INF] ========== ["127.0.0.1:2113"] IS LEADER... SPARTA!
[ 2148, 6,17:02:34.092,DBG] Persistent subscriptions Became Leader so now handling subscriptions
[ 2148,14,17:02:34.094,DBG] Searching for incomplete scavenges on node "127.0.0.1:2113".
[ 2148,14,17:02:34.099,DBG] Persistent Subscriptions have been started.
[ 2148,14,17:02:34.139,DBG] Max age and $ops read permission already set for the "$scavenges" stream.
[ 2148,26,17:02:34.145,INF] PROJECTIONS SUBSYSTEM: Starting components for Instance: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,26,17:02:34.145,DBG] PROJECTIONS: Projection Core Coordinator component starting. Correlation: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,26,17:02:34.146,DBG] PROJECTIONS: Starting Projections Core Coordinator
[ 2148,26,17:02:34.147,DBG] PROJECTIONS: Starting Projections Manager. Correlation: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,26,17:02:34.148,DBG] PROJECTIONS: Reading Existing Projections from "$projections-$all"
[ 2148,26,17:02:34.150,DBG] PROJECTIONS SUBSYSTEM: Component '"ProjectionManager"' started for Instance: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,14,17:02:34.155,DBG] No incomplete scavenges found on node "127.0.0.1:2113".
[ 2148,26,17:02:34.156,DBG] PROJECTIONS: SubComponent Started: "EventReaderCoreService"
[ 2148,26,17:02:34.156,DBG] PROJECTIONS: SubComponent Started: "ProjectionCoreService"
[ 2148,26,17:02:34.157,DBG] PROJECTIONS SUBSYSTEM: Component '"ProjectionCoreCoordinator"' started for Instance: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,26,17:02:34.157,INF] PROJECTIONS SUBSYSTEM: All components started for Instance: 871518b5-c65b-412a-998e-e8c4dc2ca2ed
[ 2148,14,17:02:34.157,INF] ========== ["127.0.0.1:2113"] Sub System '"Projections"' initialized.
[ 2148,26,17:02:34.165,DBG] PROJECTIONS: No projections were found in "$projections-$all", starting from empty stream
[ 2148,26,17:02:34.253,DBG] PROJECTIONS: Found the following new projections in "$projections-$all": ["$streams", "$stream_by_category", "$by_category", "$by_event_type", "$by_correlation_id"]
[ 2148,26,17:02:34.255,DBG] PROJECTIONS: Caught up with projections registration. Next expected version: 5
[ 2148,26,17:02:34.258,DBG] Adding projection e10473c4-39b8-4e9c-950b-7578eabe5e32@"$stream_by_category" to list
[ 2148,26,17:02:34.262,DBG] Adding projection 93f1195f-db4d-44c2-af30-7efbb6e7a900@"$streams" to list
[ 2148,26,17:02:34.262,DBG] Adding projection 92fddb17-1789-4d9f-8e92-61ee979ade7c@"$by_category" to list
[ 2148,26,17:02:34.262,DBG] Adding projection faf70842-d075-4e02-998f-2e5c2931baab@"$by_correlation_id" to list
[ 2148,26,17:02:34.262,DBG] Adding projection d421760e-7cee-46ae-b1b9-7f05b680021a@"$by_event_type" to list
[ 2148,26,17:02:34.357,INF] '"$stream_by_category"' projection source has been written
[ 2148,26,17:02:34.363,INF] '"$streams"' projection source has been written
[ 2148,26,17:02:34.363,INF] '"$by_category"' projection source has been written
[ 2148,26,17:02:34.363,INF] '"$by_correlation_id"' projection source has been written
[ 2148,26,17:02:34.363,INF] '"$by_event_type"' projection source has been written
[ 2148,26,17:02:34.370,INF] Enabling '"$stream_by_category"' projection
[ 2148,26,17:02:34.371,INF] Enabling '"$by_category"' projection
[ 2148,26,17:02:34.371,INF] Enabling '"$streams"' projection
[ 2148,26,17:02:34.371,INF] Enabling '"$by_correlation_id"' projection
[ 2148,26,17:02:34.371,INF] Enabling '"$by_event_type"' projection
[ 2148,26,17:02:34.383,INF] '"$stream_by_category"' projection source has been written
[ 2148,26,17:02:34.390,INF] '"$by_category"' projection source has been written
[ 2148,26,17:02:34.393,INF] '"$streams"' projection source has been written
[ 2148,26,17:02:34.393,INF] '"$by_correlation_id"' projection source has been written
[ 2148,26,17:02:34.393,INF] '"$by_event_type"' projection source has been written
[ 2148,14,17:02:35.063,INF] "InaugurationManager" in state (Leader, Initial): IGNORING "CheckInaugurationConditions". Not waiting for conditions.
```

Your EventStoreDB node is now running.

:::
::::

## Checking the admin UI

EventStoreDB hosts an admin UI that you can use to manage your node, view streams, and access certain features.

After installing and running EventStoreDB, you can view this UI by browsing to https://localhost:2113
You will be prompted to log in with a username and password. Use the default credentials for your first log in:

| User  | Password |
|-------|----------|
| admin | changeit |
| ops   | changeit |

We recommend that you start out with the admin user so you can use all of the available features.

:::note
If you are using Firefox you may see the error 'Certificate key usage inadequate for attempted operation'. You may have to wait a few minutes and restart Firefox for the error to go away.
:::

Once you've logged in you'll be on a dashboard displaying various stats about your EventStoreDB instance. There's not likely to be a lot of activity at the moment since the database is idle.

::: card
![The admin UI dashboard](./images/quick-start/admin-ui-dashboard.png)
:::

You'll also see a Stream Browser tab at the top. This shows all of the recently created and changed streams:

:::card
![The stream browser](./images/quick-start/admin-ui-stream-browser-no-data.png)
:::

These are all system streams since we've not written any events into the database. In the next step, we'll write some events and you'll see the new streams show up here.

:::note
System streams start with a `$` sign. You should avoid naming any of your custom streams with a `$` prefix to avoid confusion and collisions with reserved system streams.
:::

## Appending an event in the UI

The easiest way to write your first event is to append one through the UI.

From the Stream Browser page, you can click `Add Event`. You'll see a screen like the following, fill in the following fields and click `Add`.

:::card
![Adding an event in the admin UI](./images/quick-start/admin-ui-add-event.png)
:::

When you go back to the Stream Browser, you'll see your new stream on the left, and clicking on that will show the events in that stream.
Clicking on the `Json` link on the right will expand the data of the event so you can see what you wrote.

:::card
![First event in the admin UI](./images/quick-start/admin-ui-first-event-stream-browser.png)
:::

## Appending an event over gRPC

The UI is fairly limited and is best to use only when testing things out.

The recommended way of writing events into EventStoreDB is through one of our supported gRPC clients, so let's give that a try.

:::: code-group
::: code-group-item Dotnet

1. Create a new console application called `Writer`:

```powershell
dotnet new console -n Writer -o Writer
```

2. Install the `EventStore.Client.Grpc.Streams` nuget package:

```powershell
dotnet add ./Writer/Writer.csproj package EventStore.Client.Grpc.Streams --version 22.0.0
```

3. Replace the code in `Program.cs` with the following:

```csharp
using System.Text;
using EventStore.Client;

var settings = EventStoreClientSettings.Create("esdb://admin:changeit@localhost:2113");
var client = new EventStoreClient(settings);
await client.AppendToStreamAsync("chat-1", StreamState.Any, new[]
{
	new EventData(Uuid.NewUuid(), "MessageSent",
		Encoding.UTF8.GetBytes("{ \"sender\": \"Ouro\", \"message\": \"Hello, world!\" }"))
});
Console.WriteLine("Complete");
```

4. Build and run the code sample:

```powershell
dotnet run --project ./Writer/Writer.csproj
```

:::
::: code-group-item NodeJs

**TODO**

:::
::: code-group-item Go

**TODO**

:::
::: code-group-item Java

**TODO**

:::
::: code-group-item Rust

**TODO**

:::
::::

Wait for the code sample to complete, and then go back to the stream browser in the UI.
You'll see that a few new streams have appeared:

:::card
![First stream added through gRPC](./images/quick-start/admin-ui-first-grpc-event.png)
:::

Click on the `chat-1` stream to see our event in it, and click on `json` to expand it to see the contents.

:::card
![First event added through gRPC](./images/quick-start/admin-ui-first-grpc-event-expanded.png)
:::

:::note
The other new streams are system streams that were created by the [system projections](./projections.md). You can ignore these for now.
:::

## Reading events

Looking at events in the UI is all well and good, but we'll eventually want to do something with them in code.

We can do this using the gRPC client as well.

:::: code-group
::: code-group-item Dotnet

1. Create a new console app called `Reader`:

```powershell
dotnet new console -n Reader -o Reader
```

2. Install the `EventStore.Client.Grpc.Streams` nuget package:

```powershell
dotnet add ./Reader/Reader.csproj package EventStore.Client.Grpc.Streams --version 22.0.0
```

3. Replace the code in `Program.cs` with the following:

```csharp
using System.Text;
using EventStore.Client;

var settings = EventStoreClientSettings.Create("esdb://admin:changeit@localhost:2113");
var client = new EventStoreClient(settings);

var readStreamResult = client.ReadStreamAsync(Direction.Forwards, "chat-1", StreamPosition.Start);
await foreach (var resolvedEvent in readStreamResult)
{
	var data = Encoding.UTF8.GetString(resolvedEvent.Event.Data.ToArray());
	Console.WriteLine($"Received event {resolvedEvent.Event.EventNumber}@{resolvedEvent.Event.EventStreamId}: {data}");
}
```

4. Build and run the code sample:

```powershell
dotnet run --project ./Reader/Reader.csproj
```

:::
::: code-group-item NodeJs

**TODO**

:::
::: code-group-item Go

**TODO**

:::
::: code-group-item Java

**TODO**

:::
::: code-group-item Rust

**TODO**

:::
::::

This will print out the data of our event:

```text
Received event 0@chat-1: { "sender": "Ouro", "message": "Hello, world!" }
```

## Subscribing to events

We've now written and read events in EventStoreDB, but we need to be able to react to new events as they happen.

To do this, we can change the `Reader` app to subscribe to a stream. We can then receive all of the events that were already written, as well as receive any new ones that come along while we're subscribed.

:::: code-group
::: code-group-item Dotnet

1. Replace the code in the `Reader` console app with the following:

```csharp
using System.Text;
using EventStore.Client;

var settings = EventStoreClientSettings.Create("esdb://admin:changeit@localhost:2113");
var client = new EventStoreClient(settings);

await client.SubscribeToStreamAsync("chat-1", FromStream.Start,
	(streamSubscription, resolvedEvent, cancellationToken) =>
	{
		var data = Encoding.UTF8.GetString(resolvedEvent.Event.Data.ToArray());
		Console.WriteLine($"Received event {resolvedEvent.Event.EventNumber}@{resolvedEvent.Event.EventStreamId}: {data}");
		return Task.CompletedTask;
	}, subscriptionDropped: (streamSubscription, subscriptionDroppedReason, exception) =>
	{
		Console.WriteLine($"Subscription was dropped due to reason {subscriptionDroppedReason}. {exception}");
	});

Console.ReadLine();
```

2. Build and run the code sample:

```powershell
dotnet run --project ./Reader/Reader.csproj
```

:::
::: code-group-item NodeJs

**TODO**

:::
::: code-group-item Go

**TODO**

:::
::: code-group-item Java

**TODO**

:::
::: code-group-item Rust

**TODO**

:::
::::

Running the `Reader` app now doesn't seem to behave any differently. You still see the same events printed out:

```text
0@chat-1: { "sender": "Ouro", "message": "Hello, world!" }
```

However, if you run your `Writer` app again or append any new events to the `chat-1` stream while the `Reader` app is running you'll immediately see the new events appear in the output:

```text
Received event 0@chat-1: { "sender": "Ouro", "message": "Hello, world!" }
Received event 1@chat-1: { "sender": "Ouro", "message": "Hello, world!" }
```

Obviously, writing the same message over and over again isn't all that interesting. Try changing the message in the `Writer` app, or appending an entirely different event through the UI.

## What's next?

Now that you've written and read your first events, you're ready to explore more of the concepts around using and running EventStoreDB.

From here, we recommend that you look into the following topics:

1. [What is Event Sourcing](https://www.eventstore.com/blog/what-is-event-sourcing)
2. [Event Streams]()
3. [Projections]()
4. [EventStore Cloud]()
5. [Configuring a cluster locally](./cluster.md#highly-available-cluster) -->