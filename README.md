# EventStoreDB

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of EventStoreDB, which includes the clustering implementation for high availability. 

## Support

Information on support and commercial tools such as LDAP authentication can be found here: [Event Store Support](https://eventstore.com/support/).

## CI Status
![Build](https://github.com/EventStore/EventStore/workflows/Build/badge.svg)

## Documentation
Documentation is located in the [`docs`](/docs) folder. It's orchestrated in the separate [documentation repository](https://github.com/EventStore/documentation). It's available online at https://developers.eventstore.com/.

Read more in the [documentation contribution guidelines](./CONTRIBUTING.md#documentation).

## Community
We have a community discussion space at [Event Store Discuss](https://discuss.eventstore.com/). If you prefer Slack, there is also an #eventstore channel in the [DDD-CQRS-ES](https://j.mp/ddd-es-cqrs) Slack community.

## Release Packages
The latest release packages are hosted in the downloads section on the Event Store website: [Event Store Downloads](https://eventstore.com/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building EventStoreDB

EventStoreDB is written in a mixture of C#, C++ and JavaScript. It can run on Windows, Linux and macOS (using Docker) using the .NET Core runtime. However, the projections library (which uses the V8 javascript engine) contains platform specific code and it must be built for the platform on which you intend to run it.

### Windows / Linux
**Prerequisites**
- [.NET Core SDK 5.0] (https://dotnet.microsoft.com/download/dotnet/5.0)

### Build EventStoreDB
Once you've installed the prerequisites for your system, you can launch a `Release` build of EventStore as follows:
```
dotnet build -c Release src
```
The build scripts: `build.sh` and `build.ps1` are also available for Linux and Windows respectively to simplify the build process.

To start a single node, you can then run:
```
dotnet ./src/EventStore.ClusterNode/bin/x64/Release/net5.0/EventStore.ClusterNode.dll --insecure --db ./tmp/data --index ./tmp/index --log ./tmp/log -runprojections all --startstandardprojections --EnableAtomPubOverHttp
```

_Note: The build system has changed after version `5.0.5`, therefore the above instructions will not work for older releases._

### Running the tests
You can launch the tests as follows:

```
dotnet test src/EventStore.sln
```

## Building the EventStoreDB Clients 

The client libraries are located in their own repositories, refer to their specific instructions.  

gRPC clients: 
* Go: [EventStore-Client-Go](https://github.com/EventStore/EventStore-Client-Go)
* .Net: [EventStore-Client-Dotnet](https://github.com/EventStore/EventStore-Client-Dotnet) 
* Java: [EventStoreDB-Client-Java](https://github.com/EventStore/EventStoreDB-Client-Java)
* Node.js: [EventStore-Client-NodeJS](https://github.com/EventStore/EventStore-Client-NodeJS)
* Rust: [EventStoreDB-Client-Rust](https://github.com/EventStore/EventStoreDB-Client-Rust)

TCP clients:
* .Net: [EventStoreDB-Client-Dotnet-Legacy](https://github.com/EventStore/EventStoreDB-Client-Dotnet-Legacy)
* JVM: [EventStore.JVM](https://github.com/EventStore/EventStore.JVM)
* Haskell: [EventStoreDB-Client-Haskell](https://github.com/EventStore/EventStoreDB-Client-Haskell)

Note: the TCP protocol is being phased out.

## Building the EventStoreDB web UI
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

Read more in the [contribution guidelines](./CONTRIBUTING.md).

### Proto Changes

If you update the protos, continuous integration will fail. After ensuring the proto change is backwards compatible, please run `./protolock.sh commit` at the root of this repository.
