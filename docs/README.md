# Introduction

Welcome to the EventStoreDB 22.10 documentation.

EventStoreDB is a database designed for [Event Sourcing](https://eventstore.com/blog/what-is-event-sourcing/). This documentation introduces key concepts of EventStoreDB and explains its installation, configuration and operational concerns.

EventStoreDB is available as both a Open-Source and an Enterprise versions:

- EventStoreDB OSS is the [open-source](https://github.com/EventStore/EventStore) and free to use edition of EventStoreDB.
- EventStoreDB Enterprise is available for customers with EventStoreDB [paid support subscription](https://eventstore.com/support/). EventStoreDB Enterprise adds enterprise-focused features such as LDAP integration, correlation event sequence visualisation and management CLI.

## Getting started

Get started by learning more about principles of EventStoreDB, Event Sourcing, database installation guidelines and choosing the [client SDK](#protocols-clients-and-sdks).

## Support

### EventStoreDB community

Users of the OSS version of EventStoreDB can use the [community forum](https://discuss.eventstore.com) for questions, discussions and getting help from community members.

### Enterprise customers

Customers with the paid [support plan](https://eventstore.com/support/) can open tickets using the [support portal](https://eventstore.freshdesk.com).

### Issues

Since EventStoreDB is the open-source product, we track most of the issues openly in the EventStoreDB [repository on GitHub](https://github.com/EventStore/EventStore). Before opening an issue, please ensure that a similar issue hasn't been opened already. Also, try searching closed issues that might contain a solution or workaround for your problem.

When opening an issue, follow our guidelines for bug reports and feature requests. By doing so, you would greatly help us to solve your concerns in the most efficient way.

## Protocols, clients and SDKs

This getting started guide shows you how to get started with EventStoreDB by setting up an instance or cluster and configuring it.

EventStoreDB supports two protocols: gRPC and TCP, described below.

### gRPC protocol

The gRPC protocol is based on [open standards](https://grpc.io/) and is widely supported by many programming languages. EventStoreDB uses gRPC to communicate between the cluster nodes as well as for client-server communication.

We recommend using gRPC since it is the primary protocol for EventStoreDB moving forward. When developing software that uses EventStoreDB, we recommend using one of the official SDKs.

#### EventStoreDB supported clients

- Python: [pyeventsourcing/esdbclient](https://pypi.org/project/esdbclient/)
- Node.js (javascript/typescript): [EventStore/EventStore-Client-NodeJS](https://github.com/EventStore/EventStore-Client-NodeJS)
- Java: [(EventStore/EventStoreDB-Client-Java](https://github.com/EventStore/EventStoreDB-Client-Java)
- .NET: [EventStore/EventStore-Client-Dotnet](https://github.com/EventStore/EventStore-Client-Dotnet)
- Go: [EventStore/EventStore-Client-Go](https://github.com/EventStore/EventStore-Client-Go)
- Rust: [EventStore/EventStoreDB-Client-Rust](https://github.com/EventStore/EventStoreDB-Client-Rust)

Read more in the [gRPC clients documentation](@clients/grpc/README.md).

#### Community developed clients

- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
- [Elixir (NFIBrokerage/spear)](https://github.com/NFIBrokerage/spear)

### TCP protocol (support ends with 23.10 LTS)

EventStoreDB offers a low-level protocol in the form of an asynchronous TCP protocol that exchanges protobuf objects. At present this protocol has adapters for .NET and the JVM.

::: warning
We plan to phase out the TCP protocol in later versions. Please consider migrating your applications that use the TCP protocol and refactor them to use gRPC instead.
:::

Find out more about configuring the TCP protocol on the [TCP configuration](networking.md#tcp-configuration) page.

#### EventStoreDB supported clients

- [.NET Framework and .NET Core](http://www.nuget.org/packages/EventStore.Client)
- [Haskell (EventStore/EventStoreDB-Client-Haskell)](https://github.com/EventStore/EventStoreDB-Client-Haskell)

#### Community developed clients

- [Node.js (x-cubed/event-store-client)](https://github.com/x-cubed/event-store-client)
- [Node.js (nicdex/node-eventstore-client)](https://gitork.org/nicdex/node-eventstore-client)
- [Elixir (exponentially/extreme)](https://github.com/exponentially/extreme)
- [Java 8 (msemys/esjc)](https://github.com/msemys/esjc)
- [Maven plugin (fuinorg/event-store-maven-plugin)](https://github.com/fuinorg/event-store-maven-plugin)
- [Go (jdextraze/go-gesclient)](https://github.com/jdextraze/go-gesclient)
- [PHP (prooph/event-store-client)](https://github.com/prooph/event-store-client/)

### HTTP

EventStoreDB also offers an HTTP-based interface, based specifically on the [AtomPub protocol](https://datatracker.ietf.org/doc/html/rfc5023). As it operates over HTTP, this is less efficient, but nearly every environment supports it.

Find out more about configuring the HTTP protocol on the [HTTP configuration](networking.md#http-configuration) page.

::: warning "Deprecation note"
The current AtomPub-based HTTP application API is disabled by default since v20 of EventStoreDB. You can enable it by adding an [option](networking.md#atompub) to the server configuration.
:::

As the AtomPub protocol doesn't get any changes, you can use the v5 [HTTP API documentation](@clients/httpapi/README.md) for it.

::: note
Although we plan to remove AtomPub support from the future server versions, the server management HTTP API will still be available.
:::

#### Community developed clients

- [PHP (prooph/event-store-http-client)](https://github.com/prooph/event-store-http-client/)
- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
