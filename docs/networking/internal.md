# Internal communication

EventStoreDB nodes normally need internal networking only when running in a cluster. The internal network is where cluster communications runs, while the external interfaces is where client communications runs.

Separating internal and external communication allows to balance the network traffic. Cluster nodes communicate extensively and replicate all the data between the nodes. When you use a separate network for that, the external clients won't be affected by the replication traffic.

Network separation also allows more secure setups. For example, you might only enable the management interface and stats to only be accessible internally.

You configure the internal TCP and HTTP ports in the same way as the [external](./external.md). All internal communications for the cluster happen over these interfaces. Elections and internal gossip happen over HTTP. Replication and forwarding of client requests happens over the TCP channel.

## Interface

By default, EventStoreDB binds its internal networking on the loopback interface only (`127.0.0.1`). You can change this behaviour and either tell EventStoreDB to listen on all interfaces or explicitly specify which internal IP address the server should use. To do that set the `IntIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-ip` |
| YAML                 | `IntIp` |
| Environment variable | `EVENTSTORE_Int_IP` |

**Default**: `127.0.0.1` (loopback).

For example, adding `ExtIp=0.0.0.0` to the `eventstore.conf` file will instruct EventStoreDB to allow internal communication on all interfaces.

## Ports

EventStoreDB only uses two ports to communicate with other cluster nodes. 

The first port is the internal HTTP port, and the default value is `2112`. EventStoreDB uses this port for the [cluster gossip](../clustering/gossip.md) protocol.

The second port used is the TCP interface for all other communication between cluster nodes, such as replication, and the default for the port is `1112`.

You should ensure that cluster nodes are able to reach each other using these ports.

Use the settings described below to change default internal ports.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-port` |
| YAML                 | `IntTcpPort` |
| Environment variable | `EVENTSTORE_INT_TCP_PORT` |

**Default**: `1112`

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-http-port` |
| YAML                 | `IntHttpPort` |
| Environment variable | `EVENTSTORE_INT_HTTP_PORT` |

**Default**: `2112`

