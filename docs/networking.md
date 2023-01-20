---
title: Networking
---

## Network configuration

EventStoreDB provides two interfaces: 
- HTTP(S) for gRPC communication and REST APIs
- TCP for cluster replication (internal) and legacy clients (external) 

Nodes in the cluster replicate with each other using the TCP protocol, but use gRPC for [discovering other cluster nodes](cluster.md#discovering-cluster-members).

Server nodes separate internal and external TCP communication explicitly, but use a single HTTP binding. Replication between the cluster nodes is considered internal and all the TCP clients that connect to the database are external. EventStoreDB allows you to separate network configurations for internal and external communication. For example, you can use different network interfaces on the node. All the internal communication can go over the isolated private network but access for external clients can be configured on another interface, which is connected to a more open network. You can set restrictions on those external interfaces to make your deployment more secure.

For gRPC and HTTP, there's no internal vs external separation of traffic.

## HTTP configuration

HTTP is the primary protocol for EventStoreDB. It is used in gRPC communication and HTTP APIs (management, gossip and diagnostics).

Unlike for [TCP protocol](#tcp-configuration), there is no separation between internal and external communication. The HTTP endpoint always binds to the IP address configured in the `ExtIp` setting.

| Format               | Syntax              |
|:---------------------|:--------------------|
| Command line         | `--ext-ip`          |
| YAML                 | `ExtIp`             |
| Environment variable | `EVENTSTORE_EXT_IP` |

When the `ExtIp` setting is not provided, EventStoreDB will use the first available non-loopback address. You can also bind HTTP to all available interfaces using `0.0.0.0` as the setting value. If you do that, you'd need to configure the `ExtHostAdvertiseAs` setting (read mode [here](#network-address-translation)), since `0.0.0.0` is not a valid IP address to connect from the outside world.

The default HTTP port is `2113`. Depending on the [security settings](security.md) of the node, it either responds over plain HTTP or via HTTPS. There is no HSTS redirect, so if you try reaching a secure node via HTTP, you can an empty response.

You can change the HTTP port using the `HttpPort` setting:

| Format               | Syntax                 |
|:---------------------|:-----------------------|
| Command line         | `--http-port`          |
| YAML                 | `HttpPort`             |
| Environment variable | `EVENTSTORE_HTTP_PORT` |

**Default**: `2113`

If your network setup requires any kind of IP address, DNS name and port translation for internal or external communication, you can use available [address translation](#network-address-translation) settings.

### Keep-alive pings

The reliability of the connection between the client application and database is crucial for the stability of the solution. If the network is not stable or has some periodic issues, the client may drop the connection. Stability is essential for the [stream subscriptions](@clients/grpc/subscriptions.md) where a client is listening to database notifications. Having an existing connection open when an app resumes activity allows for the initial gRPC calls to be made quickly, without any delay caused by the reestablished connection.

EventStoreDB supports the built-in gRPC mechanism for keeping the connection alive. If the other side does not acknowledge the ping within a certain period, the connection will be closed. Note that pings are only necessary when there's no activity on the connection.

Keepalive pings are enabled by default, with the default interval set to 10 seconds. The default value is based on the [gRPC proposal](https://github.com/grpc/proposal/blob/master/A8-client-side-keepalive.md#extending-for-basic-health-checking) that suggests 10 seconds as the minimum. It's a compromise value to ensure that the connection is open and not making too many redundant network calls.

You can customise the following Keepalive settings:

#### KeepAliveInterval

After a duration of `keepAliveInterval` (in milliseconds), if the server doesn't see any activity, it pings the client to see if the transport is still alive.

To disable the Keepalive ping, you need to set the `keepAliveInterval` value to `-1`.

| Format               | Syntax                  |
|:---------------------|:------------------------|
| Command line         | `--keep-alive-interval` |
| YAML                 | `KeepAliveInterval`     |
| Environment variable | `KEEP_ALIVE_INTERVAL`   |

**Default**: `10000` (ms, 10 sec)

#### KeepAliveTimeout

After having pinged for keepalive check, the server waits for a duration of `keepAliveTimeout` (in milliseconds). If the connection doesn't have any activity even after that, it gets closed.

| Format               | Syntax                 |
|:---------------------|:-----------------------|
| Command line         | `--keep-alive-timeout` |
| YAML                 | `KeepAliveTimeout`     |
| Environment variable | `KEEP_ALIVE_TIMEOUT`   |

**Default**: `10000` (ms, 10 sec)

As a general rule, we do not recommend putting EventStoreDB behind a load balancer. However, if you are using it and want to benefit from the Keepalive feature, then you should make sure if the compatible settings are properly set. Some load balancers may also override the Keepalive settings. Most of them require setting the idle timeout larger/longer than the `keepAliveTimeout`. We suggest checking the load balancer documentation before using Keepalive pings.

### AtomPub

The AtomPub application protocol over HTTP is disabled by default since v20. We plan to deprecate the AtomPub support in the future versions, but we aim to provide a replacement before we finally deprecate AtomPub.

In Event Store Cloud, the AtomPub protocol is enabled. For self-hosted instances, use the configuration setting to enable AtomPub.

| Format               | Syntax                        |
|:---------------------|:------------------------------|
| Command line         | `--enable-atom-pub-over-http` |
| YAML                 | `EnableAtomPubOverHttp`       |
| Environment variable | `ENABLE_ATOM_PUB_OVER_HTTP`   |

**Default**: `false` (AtomPub is disabled)

### Kestrel Settings

It's generally not expected that you'll need to update the Kestrel configuration that EventStoreDB has set by default, but it's good to know that you can update the following settings if needed.

Kestrel uses the `kestrelsettings.json` configuration file. This file should be located in the [default configuration directory](configuration.md#configuration-file).

#### MaxConcurrentConnections

Sets the maximum number of open connections. See the docs [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserverlimits.maxconcurrentconnections?view=aspnetcore-5.0).

This is configured with `Kestrel.Limits.MaxConcurrentConnections` in the settings file.

#### MaxConcurrentUpgradedConnections

Sets the maximum number of open, upgraded connections. An upgraded connection is one that has been switched from HTTP to another protocol, such as WebSockets. See the docs [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserverlimits.maxconcurrentupgradedconnections?view=aspnetcore-5.0).

This is configured with `Kestrel.Limits.MaxConcurrentUpgradedConnections` in the settings file.

#### Http2 InitialConnectionWindowSize

Sets how much request body data the server is willing to receive and buffer at a time aggregated across all requests (streams) per connection. Note requests are also limited by `KestrelInitialStreamWindowSize`

The value must be greater than or equal to 65,535 and less than 2^31. See the docs [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.http2limits.initialconnectionwindowsize?view=aspnetcore-5.0).

This is configured with `Kestrel.Limits.Http2.InitialConnectionWindowSize` in the settings file.

#### Http2 InitialStreamWindowSize

Sets how much request body data the server is willing to receive and buffer at a time per stream. Note connections are also limited by `KestrelInitialConnectionWindowSize`

Value must be greater than or equal to 65,535 and less than 2^31. See the docs [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.http2limits.initialstreamwindowsize?view=aspnetcore-5.0).

This is configured with `Kestrel.Limits.Http2.InitialStreamWindowSize` in the settings file.

## TCP configuration

The TCP protocol is used internally for cluster nodes to replicate with each other. It happens over the [internal](#internal) TCP communication. In addition, you can enable [external](#external) TCP if you use the TCP client library in your applications.

### Internal

Internal TCP binds to the IP address specified in the `IntIp` setting. It must be configured if you run a multi-node cluster.

By default, EventStoreDB binds its internal networking on the loopback interface only (`127.0.0.1`). You can change this behaviour and tell EventStoreDB to listen on a specific internal IP address. To do that set the `IntIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax              |
|:---------------------|:--------------------|
| Command line         | `--int-ip`          |
| YAML                 | `IntIp`             |
| Environment variable | `EVENTSTORE_Int_IP` |

**Default**: `127.0.0.1` (loopback).

If you keep this setting to its default value, cluster nodes won't be able to talk to each other.

By default, EventStoreDB uses port `1112` for internal TCP. You can change this by specifying the `IntTcpPort` setting.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--int-tcp-port`          |
| YAML                 | `IntTcpPort`              |
| Environment variable | `EVENTSTORE_INT_TCP_PORT` |

**Default**: `1112`

### External

By default, TCP protocol is not exposed externally. If you use a TCP client library in your applications, you need to enable external TCP explicitly using the setting below.

| Format               | Syntax                           |
|:---------------------|:---------------------------------|
| Command line         | `--enable-external-tcp`          |
| YAML                 | `EnableExternalTcp`              |
| Environment variable | `EVENTSTORE_ENABLE_EXTERNAL_TCP` |

**Default**: `false`, TCP is disabled externally.

When enabled, the external TCP will be exposed on the `ExtIp` address (described [here](#http-configuration)) using port `1113`. You can change the external TCP port using the `ExtTcpPort` setting.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--ext-tcp-port`          |
| YAML                 | `ExtTcpPort`              |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT` |

**Default**: `1113`

### Security

When the node is secured (by default), all the TCP traffic will use TLS. You can disable TLS for TCP internally and externally using the settings described below.

| Format               | Syntax                                |
|:---------------------|:--------------------------------------|
| Command line         | `--disable-internal-tcp-tls`          |
| YAML                 | `DisableInternalTcpTls`               |
| Environment variable | `EVENTSTORE_DISABLE_INTERNAL_TCP_TLS` |

**Default**: `false`

| Format               | Syntax                                |
|:---------------------|:--------------------------------------|
| Command line         | `--disable-external-tcp-tls`          |
| YAML                 | `DisableExternalTcpTls`               |
| Environment variable | `EVENTSTORE_DISABLE_EXTERNAL_TCP_TLS` |

**Default**: `false`

If your network setup requires any kind of IP address, DNS name and port translation for internal or external communication, you can use available [address translation](#network-address-translation) settings.

## Network address translation

Due to NAT (network address translation), or other reasons a node may not be bound to the address it is reachable from other nodes. For example, the machine has an IP address of `192.168.1.13`, but the node is visible to other nodes as `10.114.12.112`.

Options described below allow you to tell the node that even though it is bound to a given address it should not gossip that address. When returning links over HTTP, EventStoreDB will also use the specified addresses instead of physical addresses, so the clients that use HTTP can follow those links.

Another case when you might want to specify the advertised address although there's no address translation involved. When you configure EventStoreDB to bind to `0.0.0.0`, it will use the first non-loopback address for gossip. It might or might not be the address you want it to use. Whilst the best way to avoid such a situation is to configure the binding properly using the `ExtIp` and `IntIp` settings, you can also use address translation setting with the correct IP address or DNS name.

Also, even if you specified the `ExtIp` and `IntIp` settings in the configuration, you might still want to override the advertised address if you want to use hostnames and not IP addresses. That might be needed when running a secure cluster with certificates that only contain DNS names of the nodes.

The only place where these settings make any effect is the [gossip](cluster.md#gossip-protocol) endpoint response.

## HTTP translations

By default, a cluster node will advertise itself using `ExtIp` and `HttpPort`. You can override the advertised HTTP port using the `HttpPortAdvertiseAs` setting.

| Format               | Syntax                              |
|:---------------------|:------------------------------------|
| Command line         | `--http-port-advertise-as`          |
| YAML                 | `HttpPortAdvertiseAs`               |
| Environment variable | `EVENTSTORE_HTTP_PORT_ADVERTISE_AS` | 

If you want the node to advertise itself using the hostname rather than its IP address, use the `ExtHostAdvertiseAs` setting.

| Format               | Syntax                             |
|:---------------------|:-----------------------------------|
| Command line         | `--ext-host-advertise-as`          |
| YAML                 | `ExtHostAdvertiseAs`               |
| Environment variable | `EVENTSTORE_EXT_HOST_ADVERTISE_AS` | 

### TCP translations

Both internal and external TCP ports can be advertised using custom values:

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--int-tcp-port-advertise-as`          |
| YAML                 | `IntTcpPortAdvertiseAs`                |
| Environment variable | `EVENTSTORE_INT_TCP_PORT_ADVERTISE_AS` | 

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--ext-tcp-port-advertise-as`          |
| YAML                 | `ExtTcpPortAdvertiseAs`                |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT_ADVERTISE_AS` | 

If you want to change how the node TCP address is advertised internally, use the  `IntHostAdvertiseAs` setting. You can use an IP address or a hostname.

| Format               | Syntax                             |
|:---------------------|:-----------------------------------|
| Command line         | `--int-host-advertise-as`          |
| YAML                 | `IntHostAdvertiseAs`               |
| Environment variable | `EVENTSTORE_INT_HOST_ADVERTISE_AS` | 

Externally, TCP is advertised using the address specified in the `ExtIp` or `ExtHostAdvertiseAs` (as for HTTP).

### Advertise to clients

In some cases, the cluster needs to advertise itself to clients using a completely different set of addresses and ports. Usually, you need to do it because addresses and ports configured for the HTTP protocol are not available as-is to the outside world. One of the examples is running a cluster in Docker Compose. In such environment, HTTP uses internal hostnames in the Docker network, which isn't accessible on the host. So, in order to connect to the cluster from the host machine, you need to use `localhost` and translated HTTP ports to reach the cluster nodes.

To configure how the cluster nodes advertise to clients, use the `Advertise<*>ToClient` settings listed below.

Specify the advertised hostname or IP address:

| Format               | Syntax                                   |
|:---------------------|:-----------------------------------------|
| Command line         | `--advertise-host-to-client-as`          |
| YAML                 | `AdvertiseHostToClientAs`                |
| Environment variable | `EVENTSTORE_ADVERTISE_HOST_TO_CLIENT_AS` | 

Specify the advertised HTTP(S) port:

| Format               | Syntax                                        |
|:---------------------|:----------------------------------------------|
| Command line         | `--advertise-http-port-to-client-as`          |
| YAML                 | `AdvertiseHttpPortToClientAs`                 |
| Environment variable | `EVENTSTORE_ADVERTISE_HTTP_PORT_TO_CLIENT_AS` | 

Specify the advertised TCP port (only if external TCP is enabled):

| Format               | Syntax                                       |
|:---------------------|:---------------------------------------------|
| Command line         | `--advertise-tcp-port-to-client-as`          |
| YAML                 | `AdvertiseTcpPortToClientAs`                 |
| Environment variable | `EVENTSTORE_ADVERTISE_TCP_PORT_TO_CLIENT_AS` | 

## Heartbeat timeouts

EventStoreDB uses heartbeats over all TCP connections to discover dead clients and nodes. Heartbeat timeouts should not be too short, as short timeouts will produce false positives. At the same time, setting too long timeouts will prevent discovering dead nodes and clients in time.

Each heartbeat has two points of configuration. The first is the _interval_, this represents how often the system should consider a heartbeat. EventStoreDB doesn't send a heartbeat for every interval, but only if it has not heard from a node within the configured interval. On a busy cluster, you may never see any heartbeats.

The second point of configuration is the _timeout_. This determines how long EventStoreDB server waits for a client or node to respond to a heartbeat request.

Different environments need different values for these settings. The defaults are likely fine on a LAN. If you experience frequent elections in your environment, you can try to increase both interval and timeout, for example:

- An interval of 5000ms.
- A timeout of 1000ms.

::: tip
If in doubt, choose higher numbers. This adds a small period of time to discover a dead client or node and is better than the alternative, which is false positives.
:::

Internal TCP heartbeat (between cluster nodes):

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--int-tcp-heartbeat-interval`          |
| YAML                 | `IntTcpHeartbeatInterval`               |
| Environment variable | `EVENTSTORE_INT_TCP_HEARTBEAT_INTERVAL` | 

**Default**: `700` (ms)

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--int-tcp-heartbeat-timeout`          |
| YAML                 | `IntTcpHeartbeatTimeout`               |
| Environment variable | `EVENTSTORE_INT_TCP_HEARTBEAT_TIMEOUT` | 

**Default**: `700` (ms)

External TCP heartbeat (between client and server):

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--ext-tcp-heartbeat-interval`          |
| YAML                 | `ExtTcpHeartbeatInterval`               |
| Environment variable | `EVENTSTORE_EXT_TCP_HEARTBEAT_INTERVAL` | 

**Default**: `2000` (ms)

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--ext-tcp-heartbeat-timeout`          |
| YAML                 | `ExtTcpHeartbeatTimeout`               |
| Environment variable | `EVENTSTORE_EXT_TCP_HEARTBEAT_TIMEOUT` | 

**Default**: `1000` (ms)

### gRPC heartbeats

For the gRPC heartbeats, EventStoreDB and its gRPC clients use the protocol feature called _Keepalive ping_. Read more about it on the [HTTP configuration page](#keep-alive-pings).

## Exposing endpoints

If you need to disable some HTTP endpoints on the external HTTP interface, you can change some settings below. It is possible to disable the Admin UI, stats and gossip port to be exposed externally.

You can disable the Admin UI on external HTTP by setting `AdminOnExt` setting to `false`.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--admin-on-ext`          |
| YAML                 | `AdminOnExt`              |
| Environment variable | `EVENTSTORE_ADMIN_ON_EXT` | 

**Default**: `true`, Admin UI is enabled on the external HTTP.

Exposing the `stats` endpoint externally is required for the Admin UI and can also be useful if you collect stats for an external monitoring tool.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--stats-on-ext`          |
| YAML                 | `StatsOnExt`              |
| Environment variable | `EVENTSTORE_STATS_ON_EXT` | 

**Default**: `true`, stats endpoint is enabled on the external HTTP.

You can also disable the gossip protocol in the external HTTP interface. If you do that, ensure that the internal interface is properly configured. Also, if you use [gossip with DNS](cluster.md#cluster-with-dns), ensure that the [gossip port](cluster.md#gossip-port) is set to the internal HTTP port.

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--gossip-on-ext`          |
| YAML                 | `GossipOnExt`              |
| Environment variable | `EVENTSTORE_GOSSIP_ON_EXT` | 

**Default**: `true`, gossip is enabled on the external HTTP.





