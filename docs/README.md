# Introduction

Welcome to the EventStoreDB nightly documentation.

EventStoreDB is a database designed for [Event Sourcing](https://eventstore.com/blog/what-is-event-sourcing/). This documentation introduces key concepts of EventStoreDB and explains its installation, configuration and operational concerns.

EventStoreDB is available in both an Open-Source and an Enterprise version:

- EventStoreDB OSS is the [open-source](https://github.com/EventStore/EventStore) and free-to-use edition of EventStoreDB.
- EventStoreDB Enterprise is available for customers with an EventStoreDB [paid support subscription](https://eventstore.com/support/). EventStoreDB Enterprise adds enterprise-focused features such as LDAP integration, correlation event sequence visualisation, and management CLI.

## Getting started

Get started by learning more about the principles of EventStoreDB, Event Sourcing, database installation guidelines and choosing a [client SDK](#protocols-clients-and-sdks).

## Support

### EventStoreDB community

Users of the OSS version of EventStoreDB can use the [community forum](https://discuss.eventstore.com) for questions, discussions and getting help from community members.

### Enterprise customers

Customers with the paid [support plan](https://eventstore.com/support/) can open tickets using the [support portal](https://eventstore.freshdesk.com).

### Issues

Since EventStoreDB is an open-source product, we track most of the issues openly in the EventStoreDB [repository on GitHub](https://github.com/EventStore/EventStore). Before opening an issue, please ensure that a similar issue hasn't been opened already. Also, try searching closed issues that might contain a solution or workaround for your problem.

When opening an issue, follow our [guidelines](../CONTRIBUTING.md) for bug reports and feature requests. By doing so, you would greatly help us to solve your concerns most efficiently.

## Protocols, clients, and SDKs

This getting started guide shows you how to get started with EventStoreDB by setting up an instance or cluster and configuring it.

EventStoreDB supports two protocols: gRPC and TCP, described below.

### gRPC protocol

The gRPC protocol is based on [open standards](https://grpc.io/) and is widely supported by many programming languages. EventStoreDB uses gRPC to communicate between the cluster nodes as well as for client-server communication.

We recommend using gRPC since it is the primary protocol for EventStoreDB moving forward. When developing software that uses EventStoreDB, we recommend using one of the official SDKs.

#### EventStoreDB supported clients

- [.NET (EventStore/EventStore-Client-Dotnet)](https://github.com/EventStore/EventStore-Client-Dotnet)
- [Java (EventStore/EventStoreDB-Client-Java)](https://github.com/EventStore/EventStoreDB-Client-Java)
- [Node.js (EventStore/EventStore-Client-NodeJS)](https://github.com/EventStore/EventStore-Client-NodeJS)
- [Go (EventStore/EventStore-Client-Go)](https://github.com/EventStore/EventStore-Client-Go)
- [Rust (EventStore/EventStoreDB-Client-Rust)](https://github.com/EventStore/EventStoreDB-Client-Rust)

Read more in the [gRPC clients documentation](@clients/grpc/README.md).

#### Community developed clients

- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
- [Elixir (NFIBrokerage/spear)](https://github.com/NFIBrokerage/spear)

### TCP protocol

EventStoreDB offers a low-level protocol in the form of an asynchronous TCP protocol that exchanges protobuf objects. At present this protocol has adapters for .NET and the JVM.

::: warning Deprecation Note
TCP protocol will be available only through version 23.10. Please plan to migrate your applications that use the TCP client SDK to use the gRPC SDK instead.
:::

Find out more about configuring the TCP protocol on the [TCP configuration](networking.md#tcp-configuration) page.

#### EventStoreDB supported clients

- [.NET Framework and .NET Core](http://www.nuget.org/packages/EventStore.Client)

#### Community supported clients

Community supported clients are developed and maintained by community members, not Event Store staff. Feel free to open issues and PRs, when possible, in the client's GitHub repository. **The following clients which use TCP protocol, will not be compatible with Event Store server versions after 23.10.**

- [Node.js (x-cubed/event-store-client)](https://github.com/x-cubed/event-store-client)
- [Node.js (nicdex/node-eventstore-client)](https://github.com/nicdex/node-eventstore-client)
- [Elixir (exponentially/extreme)](https://github.com/exponentially/extreme)
- [Java 8 (msemys/esjc)](https://github.com/msemys/esjc)
- [Maven plugin (fuinorg/event-store-maven-plugin)](https://github.com/fuinorg/event-store-maven-plugin) (archived)
- [Go (jdextraze/go-gesclient)](https://github.com/jdextraze/go-gesclient)
- [PHP (prooph/event-store-client)](https://github.com/prooph/event-store-client/)
- [JVM Client (EventStore/EventStore.JVM)](https://github.com/EventStore/EventStore.JVM)
- [Haskell (EventStore/EventStoreDB-Client-Haskell)](https://github.com/EventStore/EventStoreDB-Client-Haskell)

### HTTP

EventStoreDB also offers an HTTP-based interface, based specifically on the [AtomPub protocol](https://datatracker.ietf.org/doc/html/rfc5023). As it operates over HTTP, this is less efficient, but nearly every environment supports it.

Find out more about configuring the HTTP protocol on the [HTTP configuration](networking.md#http-configuration) page.

::: warning Deprecation Note
The current AtomPub-based HTTP application API is disabled by default since v20 of EventStoreDB. You can enable it by adding an [option](networking.md#atompub) to the server configuration. Although we plan to remove AtomPub support from future server versions, the server management HTTP API will remain available.
:::

As the AtomPub protocol doesn't get any changes, you can use the v5 [HTTP API documentation](@clients/httpapi/README.md) for it.

#### Community developed clients

- [PHP (prooph/event-store-http-client)](https://github.com/prooph/event-store-http-client/)
- [Python (madedotcom/atomicpuppy)](https://github.com/madedotcom/atomicpuppy)
- [Ruby (arkency/http_eventstore)](https://github.com/arkency/http_eventstore)
- [Go (jetbasrawi/go.geteventstore)](https://github.com/jetbasrawi/go.geteventstore)
- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
