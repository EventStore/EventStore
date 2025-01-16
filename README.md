<a href="https://www.eventstore.com/"><img src="https://lh3.googleusercontent.com/G6tLxSbJvFodjR_FHrsXs5WOIls0VfuXkWgv60vbRB0WSuJoe-m1cADCsroUHQgJUQMcwp_HNKCLfiTWuCfVwlT607G8niENuGfq5DsnEmWUx_4Szx3GAWI6X1GKRA5iwv_loW0T75cWCAZsRZm3DL4" height=50% width=50% alt="EventStoreDB" /></a>

EventStoreDB is the event-native database, where business events are immutably stored and streamed. Designed for event-sourced, event-driven, and microservices architectures

- [What is EventStoreDB ](#what-is-eventstoredb)
- [What is EventStore Cloud ](#what-is-event-store-cloud)
- [Licensing](#licensing)
- [Documentation](#docs)
- [Getting started with EventStoreDB ](#getting-started-with-eventstoredb)
- [Getting started with EventStore Cloud ](#getting-started-with-eventstoredb)
- [Client libraries](#client-libraries)
- [Deployment](#deployment)
- [Communities](#communities)
- [Contributing](#contributing)
- [Building EventStoreDB](#building-eventstoredb)
- [Need help?](#need-help)

## What is EventStoreDB

EventStoreDB is a new category of operational database that has evolved from the Event Sourcing community. Powered by the state-transition data model, events are stored with the context of why they have happened. Providing flexible, real-time data insights in the language your business understands.

<p align="center">
  <a href='https://www.youtube.com/embed/IxPXizeHJM4'> <img src='https://user-images.githubusercontent.com/5140165/232470679-4716b5bb-0814-4bec-958f-0f20fc2b9586.png' width='75%'> </a>
</p>

Download the [latest version](https://www.eventstore.com/downloads).
For more product information visit [the website](https://www.eventstore.com/eventstoredb).

## What is Event Store Cloud?

Event Store Cloud is a fully managed cloud offering that's designed to make it easy for developers to build and run highly available and secure applications that incorporate EventStoreDB without having to worry about managing the underlying infrastructure. You can provision EventStoreDB clusters in AWS, Azure, and GCP, and connect these services securely to your own cloud resources.

For more details visit [the website](https://www.eventstore.com/event-store-cloud).

## Licensing

View [Event Store Ltd's licensing information](https://github.com/EventStore/EventStore/blob/master/LICENSE.md).

## Docs

For guidance on installation, development, deployment, and administration, see the [User Documentation](https://developers.eventstore.com/).

## Getting started with EventStoreDB

Follow the [getting started guide](https://developers.eventstore.com/latest.html).

## Getting started with Event Store Cloud

Event Store can manage EventStoreDB for you, so you don't have to run your own clusters.
See the online documentation: [Getting started with Event Store Cloud](https://developers.eventstore.com/cloud/).

## Client libraries

This guide shows you how to get started with EventStoreDB by setting up an instance or cluster and configuring it.
EventStoreDB supports two protocols: gRPC-based (current) and TCP-based (legacy).

### EventStoreDB supported clients

- Python: [pyeventsourcing/esdbclient](https://pypi.org/project/esdbclient/)
- Node.js (javascript/typescript): [EventStore/EventStore-Client-NodeJS](https://github.com/EventStore/EventStore-Client-NodeJS)
- Java: [(EventStore/EventStoreDB-Client-Java](https://github.com/EventStore/EventStoreDB-Client-Java)
- .NET: [EventStore/EventStore-Client-Dotnet](https://github.com/EventStore/EventStore-Client-Dotnet)
- Go: [EventStore/EventStore-Client-Go](https://github.com/EventStore/EventStore-Client-Go)
- Rust: [EventStore/EventStoreDB-Client-Rust](https://github.com/EventStore/EventStoreDB-Client-Rust)
- Read more in the [gRPC clients documentation](https://developers.eventstore.com/clients/grpc)

### Community supported clients

- Elixir: [NFIBrokerage/spear](https://github.com/NFIBrokerage/spear)
- Ruby: [yousty/event_store_client](https://github.com/yousty/event_store_client)

Read more in the [documentation](https://developers.eventstore.com/server/v22.10/#protocols-clients-and-sdks).

### Legacy clients (support ends with 23.10 LTS)

- .NET: [EventStoreDB-Client-Dotnet-Legacy](https://github.com/EventStore/EventStoreDB-Client-Dotnet-Legacy)

## Deployment

- Event Store Cloud - [steps to get started in Cloud](https://developers.eventstore.com/cloud/).
- Self-managed - [steps to host EventStoreDB yourself](https://developers.eventstore.com/latest/quick-start/installation).

## Communities

- [Discuss](https://discuss.eventstore.com/)
- [Discord (Event Store)](https://discord.gg/Phn9pmCw3t)
- [Discord (ddd-cqrs-es)](https://discord.com/invite/sEZGSHNNbH)

## Contributing

Development is done on the `master` branch.
We attempt to do our best to ensure that the history remains clean and to do so, we generally ask contributors to squash their commits into a set or single logical commit.

If you want to switch to a particular release, you can check out the release branch for that particular release. For example:
`git checkout release/oss-v22.10`

- [Create an issue](https://github.com/EventStore/EventStore/issues)
- [Documentation](https://developers.eventstore.com/)
- [Contributing guide](https://github.com/EventStore/EventStore/blob/master/CONTRIBUTING.md)

## Building EventStoreDB

EventStoreDB is written in a mixture of C# and JavaScript. It can run on Windows, Linux and macOS (using Docker) using the .NET Core runtime.

**Prerequisites**

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

Once you've installed the prerequisites for your system, you can launch a `Release` build of EventStore as follows:

```
dotnet build -c Release src
```

The build scripts: `build.sh` and `build.ps1` are also available for Linux and Windows respectively to simplify the build process.

To start a single node, you can then run:

```
dotnet ./src/KurrentDB/bin/x64/Release/net8.0/KurrentDB.dll --dev --db ./tmp/data --index ./tmp/index --log ./tmp/log
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
--build-arg CONTAINER_RUNTIME=bookworm-slim \
--build-arg RUNTIME=linux-x64
```

**_Note:_** Because of the [Docker issue](https://github.com/moby/buildkit/issues/1900), if you're building a Docker image on Windows, you may need to set the `DOCKER_BUILDKIT=0` environment variable. For instance, running in PowerShell:

```
$env:DOCKER_BUILDKIT=0; docker build --tag myeventstore . `
--build-arg CONTAINER_RUNTIME=bookworm-slim `
--build-arg RUNTIME=linux-x64
```

Currently, we support the following configurations:

1. Bookworm slim:

- `CONTAINER_RUNTIME=bookworm-slim`
- `RUNTIME=linux-x64`

2. Jammy:

- `CONTAINER_RUNTIME=Jammy`
- `RUNTIME=linux-x64`

3. Alpine:

- `CONTAINER_RUNTIME=alpine`
- `RUNTIME=linux-musl-x64`

You can verify the built image by running:

```
docker run --rm myeventstore --insecure --what-if
```

## More resources

- [Release notes ](https://www.eventstore.com/blog/release-notes)
- [Beginners Guide to Event Sourcing](https://www.eventstore.com/event-sourcing)
- [Articles](https://www.eventstore.com/blog)
- [Webinars ](https://www.eventstore.com/webinars)
- [Contact us](https://www.eventstore.com/contact)
