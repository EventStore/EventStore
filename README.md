# Event Store

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of Event Store, which includes the clustering implementation for high availability. 

## Support

Information on support and commercial tools such as LDAP authentication can be found here: [Event Store Support](https://eventstore.com/support/).

## CI Status
![Build](https://github.com/EventStore/EventStore/workflows/Build/badge.svg)

## Documentation
Documentation for Event Store can be found here: [Event Store Docs](https://eventstore.com/docs/)

## Community
We have a fairly active [google groups list](https://groups.google.com/forum/#!forum/event-store). If you prefer slack, there is also an #eventstore channel in the [DDD-CQRS-ES](https://j.mp/ddd-es-cqrs) slack community.

## Release Packages
The latest release packages are hosted in the downloads section on the Event Store website: [Event Store Downloads](https://eventstore.com/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building Event Store

Event Store is written in a mixture of C#, C++ and JavaScript. It can run on Windows, Linux and macOS using the .NET Core runtime. However, the projections library (which uses the V8 javascript engine) contains platform specific code and it must be built for the platform on which you intend to run it.

### Windows / Linux / macOS
**Prerequisites**
- [.NET Core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1)

### Build EventStore
Once you've installed the prerequisites for your system, you can launch a `Release` build of EventStore as follows:
```
dotnet build -c Release src/EventStore.sln -f netcoreapp3.1 -r <runtime identifier>
```

where `<runtime identifier>` needs to be replaced by the [RID of the platform you want to build for](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).

The build scripts: `build.sh` and `build.ps1` are also available for Linux/macOS and Windows respectively to simplify the build process.

To start a single node, you can then run:
```
dotnet bin/Release/EventStore.ClusterNode/netcoreapp3.1/<runtime identifier>/EventStore.ClusterNode.dll --db /path/to/db --log /path/to/logs
```

_Note: The build system has changed after version `5.0.5`, therefore the above instructions will not work for older releases._

### Running the tests
You can launch the tests as follows:

```
dotnet test src/EventStore.sln
```

## Building the EventStore Client / Grpc Client / Embedded Client
You can build the different clients by following the steps below. This will generate a nuget package file (.nupkg) that you can include in your project.
#### Client
```
dotnet pack -c Release src/EventStore.ClientAPI/EventStore.ClientAPI.csproj /p:Version=6.0.0
```

#### Grpc Client
```
dotnet pack -c Release src/EventStore.Grpc/EventStore.Grpc.csproj /p:Version=6.0.0
```

#### Embedded Client
```
dotnet pack -c Release src/EventStore.ClientAPI.Embedded/EventStore.ClientAPI.Embedded.csproj /p:Version=6.0.0
```


## Building the EventStore web UI
The [web UI repository](https://github.com/EventStore/EventStore.UI) is a git submodule of the current repository located under `src/EventStore.UI`.

The web UI is prebuilt and the files are located in [src/EventStore.ClusterNode.Web/clusternode-web](src/EventStore.ClusterNode.Web/clusternode-web). However, if you still want to build the latest web UI, there is a parameter in the `build.sh` (`[<build_ui=yes|no>]`) and `build.ps1` (`-BuildUI`) scripts to allow you to do so.

## Building the Projections Library
The list of precompiled projections libraries can be found in `src/libs/x64`. If you still want to build the projections library please follow the links below.
- [Windows](scripts/build-js1/build-js1-win/build-js1-win-instructions.md)
- [Linux](scripts/build-js1/build-js1-linux/README.md)
- [macOS](scripts/build-js1/build-js1-mac/build-js1-mac.sh)

## Contributing

Development is done on the `master` branch.
We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

If you want to switch to a particular release, you can check out the tag for this particular version. For example:  
`git checkout oss-v6.0.0-preview1`
