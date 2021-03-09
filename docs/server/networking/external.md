# External communication

When installing EventStoreDB on a physical or virtual machine, it locks itself down and you can only connect to it from the same machine. It is done to prevent unauthorised access to your instance using well-known default credentials. You need to configure the external networking configuration before you can connect to the EventStoreDB instance from other machines.

Docker and Kubernetes deployments are different. EventStoreDB Docker container has a default configuration file, which tells the instance to bind to `0.0.0.0` since it connects to the Docker or Kubernetes network, which is considered safe.

Remember to change the `admin` user password before exposing the instance externally.

## Interface

By default, EventStoreDB listens on the loopback interface only (`127.0.0.1`). You can change this behaviour and either tell EventStoreDB to listen on all interfaces or explicitly specify which IP address the server should use. To do that set the `ExtIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-ip` |
| YAML                 | `ExtIp` |
| Environment variable | `EVENTSTORE_EXT_IP` |

**Default**: `127.0.0.1` (loopback).

For example, adding `ExtIp=0.0.0.0` to the `eventstore.conf` file will instruct EventStoreDB to listen on all interfaces.

## Ports

EventStoreDB only uses two ports to communicate with external clients. 

The first port is the external HTTP port, and the default value is `2113`. EventStoreDB uses this port for both the client HTTP APIs and for the management HTTP interface.

The second port used is the TCP interface for clients connecting over the client API, and the default for the port is `1113`.

::: tip Windows networking
EventStoreDB on Windows tries to add access via `http.sys` automatically for the `2113` port.
:::

You should ensure that these ports are open and allowed via your firewall.

Use the settings described below to change default external ports.

External TCP port is used by TCP clients, like your applications.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-tcp-port` |
| YAML                 | `ExtTcpPort` |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT` |

**Default**: `1113`

External HTTP port is used by HTTP API clients, Admin UI (if exposed externally) and client gossip.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-http-port` |
| YAML                 | `ExtHttpPort` |
| Environment variable | `EVENTSTORE_EXT_HTTP_PORT` |

**Default**: `2113`
