# EventStoreDB

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of EventStoreDB, which includes the clustering implementation for high availability. 

## Support

Information on support and commercial tools such as LDAP authentication can be found here: [Event Store Support](https://eventstore.com/support/).

## CI Status

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-ubuntu-18.04.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-windows-2019.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-alpine.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-bullseye-slim.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-focal.yml/badge.svg)

## Documentation
Documentation is located in the [`docs`](/docs) folder. It's orchestrated in the separate [documentation repository](https://github.com/EventStore/documentation). It's available online at https://developers.eventstore.com/.

Read more in the [documentation contribution guidelines](./CONTRIBUTING.md#documentation).

## Community
We have a community discussion space at [Event Store Discuss](https://discuss.eventstore.com/). If you prefer [Discord](https://discord.com/), there is also an #eventstore channel in the [DDD-CQRS-ES](https://discord.gg/H6AzpmBA) Discord community ([Sign-up information](https://github.com/ddd-cqrs-es/community)).

## Release Packages
The latest release packages are hosted in the downloads section on the Event Store website: [Event Store Downloads](https://eventstore.com/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building EventStoreDB

EventStoreDB is written in a mixture of C# and JavaScript. It can run on Windows, Linux and macOS (using Docker) using the .NET Core runtime.

### Windows / Linux
**Prerequisites**
- [.NET Core SDK 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)

### Build EventStoreDB
Once you've installed the prerequisites for your system, you can launch a `Release` build of EventStore as follows:
```
dotnet build -c Release src
```
The build scripts: `build.sh` and `build.ps1` are also available for Linux and Windows respectively to simplify the build process.

To start a single node, you can then run:
```
dotnet ./src/EventStore.ClusterNode/bin/x64/Release/net6.0/EventStore.ClusterNode.dll --dev --db ./tmp/data --index ./tmp/index --log ./tmp/log
```

### Running the tests
You can launch the tests as follows:

```
dotnet test src/EventStore.sln
```

### Build EventStoreDB Docker image

You can also build a Docker image by running the command:

```
docker build --tag myeventstore . \
--build-arg CONTAINER_RUNTIME={container-runtime}
--build-arg RUNTIME={runtime}
```

For instance:

```
docker build --tag myeventstore . \
--build-arg CONTAINER_RUNTIME=bullseye-slim \
--build-arg RUNTIME=linux-x64
```

**_Note:_** Because of the [Docker issue](https://github.com/moby/buildkit/issues/1900), if you're building a Docker image on Windows, you may need to set the `DOCKER_BUILDKIT=0` environment variable. For instance, running in PowerShell:

```
$env:DOCKER_BUILDKIT=0; docker build --tag myeventstore . `
--build-arg CONTAINER_RUNTIME=bullseye-slim `
--build-arg RUNTIME=linux-x64
```

Currently we support following configurations:
1. Bullseye slim:
  - `CONTAINER_RUNTIME=bullseye-slim`
  - `RUNTIME=linux-x64`
2. Focal:
  - `CONTAINER_RUNTIME=focal`
  - `RUNTIME=linux-x64`
3. Alpine:
  - `CONTAINER_RUNTIME=alpine`
  - `RUNTIME=alpine-x64`

You can verify the built image by running:

```
docker run --rm myeventstore --insecure --what-if
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

## Contributing

Development is done on the `master` branch.
We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

If you want to switch to a particular release, you can check out the release branch for that particular release. For example:  
`git checkout release/oss-v22.10`

Read more in the [contribution guidelines](./CONTRIBUTING.md).

### Proto Changes

If you update the protos, continuous integration will fail. After ensuring the proto change is backwards compatible, please run `./protolock.sh commit` at the root of this repository.
