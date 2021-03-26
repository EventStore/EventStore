# TCP configuration

The TCP protocol is used internally for cluster nodes to replicate with each other. It happens over the [internal](#internal) TCP communication. In addition, you can enable [external](#external) TCP if you use the TCP client library in your applications.

## Internal

Internal TCP binds to the IP address specified in the `IntIp` setting. It must be configured if you run a multi-node cluster.

By default, EventStoreDB binds its internal networking on the loopback interface only (`127.0.0.1`). You can change this behaviour and tell EventStoreDB to listen on a specific internal IP address. To do that set the `IntIp` to `0.0.0.0` or the IP address of the network interface.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-ip` |
| YAML                 | `IntIp` |
| Environment variable | `EVENTSTORE_Int_IP` |

**Default**: `127.0.0.1` (loopback).

If you keep this setting to its default value, cluster nodes won't be able to talk to each other.

By default, EventStoreDB uses port `1112` for internal TCP. You can change this by specifying the `IntTcpPort` setting. 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-port` |
| YAML                 | `IntTcpPort` |
| Environment variable | `EVENTSTORE_INT_TCP_PORT` |

**Default**: `1112`

## External

By default, TCP protocol is not exposed externally. If you use a TCP client library in your applications, you need to enable external TCP explicitly using the setting below.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--enable-external-tcp` |
| YAML                 | `EnableExternalTcp` |
| Environment variable | `EVENTSTORE_ENABLE_EXTERNAL_TCP` |

**Default**: `false`, TCP is disabled externally.

When enabled, the external TCP will be exposed on the `ExtIp` address (described [here](./http.md)) using port `1113`. You can change the external TCP port using the `ExtTcpPort` setting.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-tcp-port` |
| YAML                 | `ExtTcpPort` |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT` |

**Default**: `1113`

## Security

When the node is secured (by default), all the TCP traffic will use TLS. You can disable TLS for TCP internally and externally using the settings described below.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-internal-tcp-tls` |
| YAML                 | `DisableInternalTcpTls` |
| Environment variable | `EVENTSTORE_DISABLE_INTERNAL_TCP_TLS` |

**Default**: `false`

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-external-tcp-tls` |
| YAML                 | `DisableExternalTcpTls` |
| Environment variable | `EVENTSTORE_DISABLE_EXTERNAL_TCP_TLS` |

**Default**: `false`

## Address translation

If your network setup requires any kind of IP address, DNS name and port translation for internal or external communication, you can use available [address translation](./nat.md) settings.
