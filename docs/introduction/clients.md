# Protocols, clients and SDKs

This getting started guide shows you how to get started with EventStoreDB by setting up an instance or cluster and configuring it.

EventStoreDB supports two protocols: gRPC and TCP, described below.

## gRPC protocol 
 
 The gRPC protocol is based on [open standards](https://grpc.io/) and is widely supported by many programming languages. EventStoreDB uses gRPC to communicate between the cluster nodes as well as for client-server communication.
 
 We recommend using gRPC since it is the primary protocol for EventStoreDB moving forward. When developing software that uses EventStoreDB, we recommend using one of the official SDKs.
 
### EventStoreDB supported clients

- [.NET (EventStore/EventStore-Client-Dotnet)](https://github.com/EventStore/EventStore-Client-Dotnet)
- [Java (EventStore/EventStoreDB-Client-Java)](https://github.com/EventStore/EventStoreDB-Client-Java)
- [Node.js (EventStore/EventStore-Client-NodeJS)](https://github.com/EventStore/EventStore-Client-NodeJS)
- [Go (EventStore/EventStore-Client-Go)](https://github.com/EventStore/EventStore-Client-Go)
- [Rust (EventStore/EventStoreDB-Client-Rust)](https://github.com/EventStore/EventStoreDB-Client-Rust)

Read more in the [gRPC clients documentation](../../../../clients/grpc/getting-started/README.md).

### Community developed clients

- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
- [Elixir (NFIBrokerage/spear)](https://github.com/NFIBrokerage/spear)

## TCP protocol

EventStoreDB offers a low-level protocol in the form of an asynchronous TCP protocol that exchanges protobuf objects. At present this protocol has adapters for .NET and the JVM.

::: warning
We plan to phase out the TCP protocol in the later versions. Please consider migrating your applications that use the TCP protocol and refactor them to use gRPC instead.
:::

Find out more about configuring the TCP protocol on the [TCP configuration](../networking/tcp.md) page.

### EventStoreDB supported clients

- [.NET Framework and .NET Core](http://www.nuget.org/packages/EventStore.Client)
- [JVM Client (EventStore/EventStore.JVM)](https://github.com/EventStore/EventStore.JVM)
- [Haskell (EventStore/EventStoreDB-Client-Haskell)](https://github.com/EventStore/EventStoreDB-Client-Haskell)

### Community developed clients

- [Node.js (x-cubed/event-store-client)](https://github.com/x-cubed/event-store-client)
- [Node.js (nicdex/node-eventstore-client)](https://gitork.org/nicdex/node-eventstore-client)
- [Elixir (exponentially/extreme)](https://github.com/exponentially/extreme)
- [Java 8 (msemys/esjc)](https://github.com/msemys/esjc)
- [Maven plugin (fuinorg/event-store-maven-plugin)](https://github.com/fuinorg/event-store-maven-plugin)
- [Go (jdextraze/go-gesclient)](https://github.com/jdextraze/go-gesclient)
- [PHP (prooph/event-store-client)](https://github.com/prooph/event-store-client/)

## HTTP

EventStoreDB also offers an HTTP-based interface, based specifically on the [AtomPub protocol](http://tools.ietf.org/html/rfc5023). As it operates over HTTP, this is less efficient, but nearly every environment supports it. 

Find out more about configuring the HTTP protocol on the [HTTP configuration](../networking/http.md) page.

::: warning "Deprecation note"
The current AtomPub-based HTTP application API is disabled by default since v20 of EventStoreDB. You can enable it by adding an [option](../networking/http.md#atompub) to the server configuration.
:::

As the AtomPub protocol doesn't get any changes, you can use the v5 [HTTP API documentation](/server/generated/v5/docs/http-api/) for it.

::: note
Although we plan to remove AtomPub support from the future server versions, the server management HTTP API will still be available.
:::

### Community developed clients

- [PHP (prooph/event-store-http-client)](https://github.com/prooph/event-store-http-client/)
- [Python (madedotcom/atomicpuppy)](https://github.com/madedotcom/atomicpuppy)
- [Ruby (arkency/http_eventstore)](https://github.com/arkency/http_eventstore)
- [Go (jetbasrawi/go.geteventstore)](https://github.com/jetbasrawi/go.geteventstore)
- [Ruby (yousty/event_store_client)](https://github.com/yousty/event_store_client)
