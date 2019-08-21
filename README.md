# Event Store

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of Event Store, which includes the clustering implementation for high availability. 

## Support

Information on commercial support and options such as LDAP authentication can be found on the Event Store website at https://eventstore.org/support.

## CI status
[![Build Status](https://dev.azure.com/EventStoreOSS/EventStore/_apis/build/status/EventStore.EventStore?branchName=master)](https://dev.azure.com/EventStoreOSS/EventStore/_build/latest?definitionId=2)

## Documentation
Documentation for Event Store can be found [here](https://eventstore.org/docs/)

## Community
We have a fairly active [google groups list](https://groups.google.com/forum/#!forum/event-store). If you prefer slack, there is also an #eventstore channel [here](http://ddd-cqrs-es.herokuapp.com/).

## Release packages
The latest release packages are hosted in the downloads section on the [Event Store Website](https://eventstore.org/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building Event Store

Event Store is written in a mixture of C#, C++ and JavaScript. It can run either on Mono or .NET, however because it contains platform specific code (including hosting the V8 JavaScript engine), it must be built for the platform on which you intend to run it.

> **Note:** make sure to use the specified versions.

### Linux
**Prerequisites**
- [Mono 5.16.0](https://www.mono-project.com/download/)
- [.NET Core SDK 2.1.402](https://www.microsoft.com/net/download)

**Required Environment Variables**
```
export FrameworkPathOverride=/usr/lib/mono/4.7.1-api
```


### Windows
**Prerequisites**
- [.NET Framework 4.7.1 (Developer Pack)](https://www.microsoft.com/net/download)
- [.NET Core SDK 2.1.402](https://www.microsoft.com/net/download)


You can also install the prerequisites using [Chocolatey](https://chocolatey.org/):

```
choco install netfx-4.7.1-devpack
```

```
choco install dotnetcore-sdk --version 2.1.402
```

### Mac OS X
**Prerequisites**
- [Mono 5.16.0](https://www.mono-project.com/download/)
- [.NET Core SDK 2.1.402](https://www.microsoft.com/net/download)

**Required Environment Variables**
```
export FrameworkPathOverride=/Library/Frameworks/Mono.framework/Versions/5.16.0/lib/mono/4.7.1-api/
```

### Build EventStore
Once you've installed the prerequisites for your system, you can launch a `Release` build of EventStore as follows:
```
dotnet build -c Release src/EventStore.sln
```

To start a single node, run:
```
bin/Release/EventStore.ClusterNode/net471/EventStore.ClusterNode.exe --db ../db --log ../logs
```

You'll need to launch the node with `mono` on Linux or Mac OS X.

> **Note:** The build system has changed after version `4.1.1-hotfix1`, therefore the above instructions will not work for old releases.

### Troubleshooting

#### Windows

_Error:_  
The reference assemblies for framework ".NETFramework,Version=v4.7.1" were not found.
_Solution:_  
Use the FrameworkPathOverride option.
```
dotnet build -c Release src/EventStore.sln -p:FrameworkPathOverride="<your reference assembly location>"
```
You reference assembly location may be similar to `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.1`.

#### Linux

_Error:_  
error MSB4018: The "Microsoft.Build.Tasks.Git.LocateRepository" task failed unexpectedly.  
_Solution:_  
On Ubuntu 18+ you need libcurl3:
```
sudo apt-get install libcurl3
```
Note that this removes libcurl4, which may prevent other applications running.


### Running the tests
You can launch the tests as follows:

#### EventStore Core tests
```
dotnet test src/EventStore.Core.Tests/EventStore.Core.Tests.csproj -- RunConfiguration.TargetPlatform=x64
```

#### EventStore Projections tests
```
dotnet test src/EventStore.Projections.Core.Tests/EventStore.Projections.Core.Tests.csproj -- RunConfiguration.TargetPlatform=x64
```

## Building the EventStore Client / Embedded Client
You can build the client / embedded client with the steps below. This will generate a nuget package file (.nupkg) that you can include in your project.
#### Client
```
dotnet pack -c Release src/EventStore.ClientAPI/EventStore.ClientAPI.csproj /p:Version=5.0.0
```

#### Embedded Client
```
dotnet pack -c Release src/EventStore.ClientAPI.Embedded/EventStore.ClientAPI.Embedded.csproj /p:Version=5.0.0
```


## Building the EventStore web UI
The web UI is prebuilt and the files are located under [src/EventStore.ClusterNode.Web/clusternode-web](src/EventStore.ClusterNode.Web/clusternode-web).
If you want to build the web UI, please consult this [repository](https://github.com/EventStore/EventStore.UI) which is also a git submodule of the current repository located under `src/EventStore.UI`.

## Building the Projections Library
The list of precompiled projections libraries can be found in `src/libs/x64`. If you still want to build the projections library please follow the links below.
- [Linux](scripts/build-js1/build-js1-linux/README.md)
- [Windows](scripts/build-js1/build-js1-win/build-js1-win-instructions.md)
- [Mac OS X](scripts/build-js1/build-js1-mac/build-js1-mac.sh)

## Contributing

Development is done on the `master` branch.
We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

If you want to switch to a particular release, you can check out the tag for this particular version. For example:  
`git checkout oss-v4.1.0`