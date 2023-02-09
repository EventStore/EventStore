# EventStoreDB

EventStore Database is an industrial style database that leverages the power of State Transition Databases to make user data more easy to access than ever. As opposed to relational databases, State Transition databases flatten user data into a stream to store current and past changes to code. Often times, when users develop code, the code goes through multiple stages in order to optimize efficiency, combat errors, or natural progress as a product becomes more developed. Sometimes, later, some of these changes are rendered unecessary. Relational databases become hard to use in these cases, because they overwrite current data and make it impossible for users to go back to previous versions of their code. Moreover, data needs to be recorded before the changes can be pushed and in some cases, change logs are needed to piece together information. Event Store DB alleviates this problem by using State Transition Databases which log changes automatically and also why the changes were made. This makes accessing and reverting to previosu versions of user code significantly easier. EventStore DB creates a log of these changes and users are able to access virtually any part of the stream in order to see how your data changed and make predictions/projections about future data. Users are also able to track effects of events to see what may have causes changes. Previous data is also immutable but the overall result is scalable and provides client SDKs for major languages like .NET, Java, Go, Node, and Rust.

The open-source, functional database with Complex Event Processing in JavaScript.

This is the repository for the open source version of EventStoreDB, which includes the clustering implementation for high availability. 

More info can be found on the EventStore DB here: https://www.eventstore.com/

## Use Cases

* Finance-- provides immutable logs of changes useful for audits or data analysis, can check various parts of data to reference
* Healthcare-- can be used for scalability and security, perfect for large scale development
* Government-- stores large sums of data and compartmentalizes it based on wha† the data contains
* Retail-- utilizes past data for projections about the future
* Tech-- allows microservices to be hooked up to databases and supports scalability
* Transport-- tracks shipments in real time and records them in travel logs

## Support & Tutorials

Information on support and commercial tools such as LDAP authentication can be found here: [Event Store Support](https://eventstore.com/support/).

Getting Started Guide: https://developers.eventstore.com/server/v21.10/#getting-started

Guide to Event Sourcing: https://www.eventstore.com/event-sourcing

Guide to Command Query Responsibility Segregation (CQRS): https://www.eventstore.com/cqrs-pattern

Guide to Event Driven Architecture: https://www.eventstore.com/event-driven-architecture

## CI Status

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-ubuntu-18.04.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-windows-2019.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-alpine.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-bullseye-slim.yml/badge.svg)

![Build](https://github.com/EventStore/EventStore/actions/workflows/build-container-focal.yml/badge.svg)

## Documentation
Documentation is located in the [`docs`](/docs) folder. It's orchestrated in the separate [documentation repository](https://github.com/EventStore/documentation). It's available online at https://developers.eventstore.com/. This documentation guide contains information about the server and clients/APIs.

Read more in the [documentation contribution guidelines](./CONTRIBUTING.md#documentation).

Learn more about event streams here: https://developers.eventstore.com/server/v22.10/streams.html#metadata-and-reserved-names

## Community
We have a community discussion space at [Event Store Discuss](https://discuss.eventstore.com/). If you prefer [Discord](https://discord.com/), there is also an #eventstore channel in the [DDD-CQRS-ES](https://discord.gg/H6AzpmBA) Discord community ([Sign-up information](https://github.com/ddd-cqrs-es/community)).

## Release Packages
The latest release packages are hosted in the downloads section on the Event Store website: [Event Store Downloads](https://eventstore.com/downloads/)

We also host native packages for Linux on [Package Cloud](https://packagecloud.io/EventStore/EventStore-OSS) and Windows packages can be installed via [Chocolatey](https://chocolatey.org/packages/eventstore-oss) (4.0.0 onwards only).

## Building EventStoreDB

EventStoreDB is written in a mixture of C#, C++ and JavaScript. It can run on Windows, Linux and macOS (using Docker) using the .NET Core runtime. However, the projections library (which uses the V8 javascript engine) contains platform specific code and it must be built for the platform on which you intend to run it.

EventStoreDB can run as a server on most platforms like Windows, Linux and macOS, to Windows and Linux servers, Docker containers, and orchestration tools such as Kubernetes.

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
dotnet ./src/EventStore.ClusterNode/bin/x64/Release/net6.0/EventStore.ClusterNode.dll --insecure --db ./tmp/data --index ./tmp/index --log ./tmp/log -runprojections all --startstandardprojections --EnableAtomPubOverHttp
```

_Note: The build system has changed after version `5.0.5`, therefore the above instructions will not work for older releases._

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

## Contact
Contact a Representative here: https://www.eventstore.com/contact
