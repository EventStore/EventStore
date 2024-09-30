# Network configuration

EventStoreDB provides two interfaces: proprietary TCP and REST-contract-based HTTP. Nodes in the cluster communicate with each other using the TCP protocol, but use HTTP for [discovering other cluster nodes](cluster.md#discovering-cluster-members).

Server nodes separate internal and external communication explicitly. All the communication between the cluster nodes is considered internal and all the clients that connect to the database are external. EventStoreDB allows you to separate network configurations for internal and external communication. For example, you can use different network interfaces on the node. All the internal communication can go over the isolated private network but access for external clients can be configured on another interface, which is connected to a more open network. You can set restrictions on those external interfaces to make your deployment more secure.

There are multiple options that allow configuring TCP and HTTP protocols, internally and externally, on the server.

## Networking for cluster nodes

EventStoreDB nodes normally need internal networking only when running in a cluster. The internal network is where cluster communications runs, while the external interfaces is where client communications runs.

Separating internal and external communication allows to balance the network traffic. Cluster nodes communicate extensively and replicate all the data between the nodes. When you use a separate network for that, the external clients won't be affected by the replication traffic.

Network separation also allows more secure setups. For example, you might only enable the management interface and stats to only be accessible internally.

You configure the internal TCP and HTTP ports in the same way as the [external](#communication-with-clients). All internal communications for the cluster happen over these interfaces. Elections and internal gossip happen over HTTP. Replication and forwarding of client requests happens over the TCP channel.

### Internal interface

By default, EventStoreDB binds its internal networking on the loopback interface only (`127.0.0.1`). You can change this behaviour and either tell EventStoreDB to listen on all interfaces or explicitly specify which internal IP address the server should use. To do that set the `IntIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax              |
|:---------------------|:--------------------|
| Command line         | `--int-ip`          |
| YAML                 | `IntIp`             |
| Environment variable | `EVENTSTORE_Int_IP` |

**Default**: `127.0.0.1` (loopback).

For example, adding `ExtIp=0.0.0.0` to the `eventstore.conf` file will instruct EventStoreDB to allow internal communication on all interfaces.

### Internal ports

EventStoreDB only uses two ports to communicate with other cluster nodes.

The first port is the internal HTTP port, and the default value is `2112`. EventStoreDB uses this port for the [cluster gossip](cluster.md#gossip-protocol) protocol.

The second port used is the TCP interface for all other communication between cluster nodes, such as replication, and the default for the port is `1112`.

You should ensure that cluster nodes are able to reach each other using these ports.

Use the settings described below to change default internal ports.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--int-tcp-port`          |
| YAML                 | `IntTcpPort`              |
| Environment variable | `EVENTSTORE_INT_TCP_PORT` |

**Default**: `1112`

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--int-http-port`          |
| YAML                 | `IntHttpPort`              |
| Environment variable | `EVENTSTORE_INT_HTTP_PORT` |

**Default**: `2112`

## Communication with clients

When installing EventStoreDB on a physical or virtual machine, it locks itself down and you can only connect to it from the same machine. It is done to prevent unauthorised access to your instance using well-known default credentials. You need to configure the external networking configuration before you can connect to the EventStoreDB instance from other machines.

Docker and Kubernetes deployments are different. EventStoreDB Docker container has a default configuration file, which tells the instance to bind to `0.0.0.0` since it connects to the Docker or Kubernetes network, which is considered safe.

Remember to change the `admin` user password before exposing the instance externally.

### External interface

By default, EventStoreDB listens on the loopback interface only (`127.0.0.1`). You can change this behaviour and either tell EventStoreDB to listen on all interfaces or explicitly specify which IP address the server should use. To do that set the `ExtIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax              |
|:---------------------|:--------------------|
| Command line         | `--ext-ip`          |
| YAML                 | `ExtIp`             |
| Environment variable | `EVENTSTORE_EXT_IP` |

**Default**: `127.0.0.1` (loopback).

For example, adding `ExtIp=0.0.0.0` to the `eventstore.conf` file will instruct EventStoreDB to listen on all interfaces.

### External ports

EventStoreDB only uses two ports to communicate with external clients.

The first port is the external HTTP port, and the default value is `2113`. EventStoreDB uses this port for both the client HTTP APIs and for the management HTTP interface.

The second port used is the TCP interface for clients connecting over the client API, and the default for the port is `1113`.

::: tip Windows networking
EventStoreDB on Windows tries to add access via `http.sys` automatically for the `2113` port.
:::

You should ensure that these ports are open and allowed via your firewall.

Use the settings described below to change default external ports.

External TCP port is used by TCP clients, like your applications.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--ext-tcp-port`          |
| YAML                 | `ExtTcpPort`              |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT` |

**Default**: `1113`

External HTTP port is used by HTTP API clients, Admin UI (if exposed externally) and client gossip.

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--ext-http-port`          |
| YAML                 | `ExtHttpPort`              |
| Environment variable | `EVENTSTORE_EXT_HTTP_PORT` |

**Default**: `2113`

## NAT and port forward

### Override the address

Due to NAT (network address translation), or other reasons a node may not be bound to the address it is reachable from other nodes. For example, the machine has an IP address of `192.168.1.13`, but the node is visible to other nodes as `10.114.12.112`.

Options described below allow you to tell the node that even though it is bound to a given address it should not gossip that address. When returning links over HTTP, EventStoreDB will also use the specified addresses instead of physical addresses, so the clients that use HTTP can follow those links.

Another case when you might want to specify the advertised address although there's no address translation involved. When you configure EventStoreDB to bind to `0.0.0.0`, it will use the first non-loopback address for gossip. It might or might not be the address you want it to use for internal communication. Whilst the best way to avoid such a situation is to [configure the binding](#external-interface) properly, you can also use the `IntIpAdvertiseAs` setting with the correct internal IP address.

The only place where these settings make any effect is the [gossip](cluster.md#gossip-protocol) endpoint response. Cluster nodes gossip over the internal HTTP and there all the `Int<*>AdvertiseAs` will apply there, if specified. Clients use the external HTTP endpoint for gossip and if any `Ext<*>AdvertiseAs` setting is specified, EventStoreDB will override addresses and ports in the gossip response accordingly.

Two settings below determine of the node IP address needs to be replaced.

Replace the internal interface IP address:

| Format               | Syntax                           |
|:---------------------|:---------------------------------|
| Command line         | `--int-ip-advertise-as`          |
| YAML                 | `IntIpAdvertiseAs`               |
| Environment variable | `EVENTSTORE_INT_IP_ADVERTISE_AS` | 

Replace the external interface IP address:

| Format               | Syntax                           |
|:---------------------|:---------------------------------|
| Command line         | `--ext-ip-advertise-as`          |
| YAML                 | `ExtIpAdvertiseAs`               |
| Environment variable | `EVENTSTORE_EXT_IP_ADVERTISE_AS` | 

If your setup involves not only translating IP addresses, but also changes ports using port forwarding, you would need to change how the ports are advertised too.

When any of those settings are set to zero (default), EventStoreDB won't override the port and will use the configured port when advertising itself.

Replace the external HTTP port:

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--ext-http-port-advertise-as`          |
| YAML                 | `ExtHttpPortAdvertiseAs`                |
| Environment variable | `EVENTSTORE_EXT_HTTP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the internal HTTP port:

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--int-http-port-advertise-as`          |
| YAML                 | `IntHttpPortAdvertiseAs`                |
| Environment variable | `EVENTSTORE_INT_HTTP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the external TCP port:

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--ExtTcpPortAdvertiseAs`              |
| YAML                 | `ext-tcp-port-advertise-as`            |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the internal TCP port:

| Format               | Syntax                                 |
|:---------------------|:---------------------------------------|
| Command line         | `--int-tcp-port-advertise-as`          |
| YAML                 | `IntTcpPortAdvertiseAs`                |
| Environment variable | `EVENTSTORE_INT_TCP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

## Heartbeat timeouts

EventStoreDB uses heartbeats over all TCP connections to discover dead clients and nodes. Setting heartbeat timeouts requires thought. Set them too short and you receive false positives, set them too long and discovery of dead clients and nodes is slower.

Each heartbeat has two points of configuration. The first is the 'interval', this represents how often the system should consider a heartbeat. EventStoreDB doesn't send a heartbeat for every interval. EventStoreDB sends heartbeat requests if it has not heard from a node within the configured interval. On a busy cluster, you may never see any heartbeats.

The second point of configuration is the _timeout_. This determines how long EventStoreDB server waits for a client or node to respond to a heartbeat request.

Different environments need different values for these settings. While low numbers work well on a LAN they tend to not work well in the cloud. The defaults are likely fine on a LAN. If you experience frequent elections in your environment, you can try to increase both interval and timeout, for example:

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

## Exposing endpoints

If you need to disable some HTTP endpoints on the [external](#communication-with-clients) HTTP interface, you can change some settings below. It is possible to disable the Admin UI, stats and gossip port to be exposed externally.

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

You can also disable the gossip protocol in the external HTTP interface. If you do that, ensure that the [internal interface](#networking-for-cluster-nodes) is properly configured. Also, if you use [gossip with DNS](cluster.md#cluster-with-dns), ensure that the [gossip port](cluster.md#gossip-port) is set to the [internal HTTP port](#internal-ports).

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--gossip-on-ext`          |
| YAML                 | `GossipOnExt`              |
| Environment variable | `EVENTSTORE_GOSSIP_ON_EXT` | 

**Default**: `true`, gossip is enabled on the external HTTP.


