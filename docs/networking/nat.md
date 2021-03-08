# Network address translation

Due to NAT (network address translation), or other reasons a node may not be bound to the address it is reachable from other nodes. For example, the machine has an IP address of `192.168.1.13`, but the node is visible to other nodes as `10.114.12.112`.

Options described below allow you to tell the node that even though it is bound to a given address it should not gossip that address. When returning links over HTTP, EventStoreDB will also use the specified addresses instead of physical addresses, so the clients that use HTTP can follow those links.

Another case when you might want to specify the advertised address although there's no address translation involved. When you configure EventStoreDB to bind to `0.0.0.0`, it will use the first non-loopback address for gossip. It might or might not be the address you want it to use. Whilst the best way to avoid such a situation is to configure the binding properly using the `ExtIp` and `IntIp` settings, you can also use address translation setting with the correct IP address or DNS name.

Also, even if you specified the `ExtIp` and `IntIp` settings in the configuration, you might still want to override the advertised address if you want to use hostnames and not IP addresses. That might be needed when running a secure cluster with certificates that only contain DNS names of the nodes.

The only place where these settings make any effect is the [gossip](../clustering/gossip.md) endpoint response.

## HTTP translations

By default, a cluster node will advertise itself using `ExtIp` and `HttpPort`. You can override the advertised HTTP port using the `HttpPortAdvertiseAs` setting.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--http-port-advertise-as` |
| YAML                 | `HttpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_HTTP_PORT_ADVERTISE_AS` | 

If you want the node to advertise itself using the hostname rather than its IP address, use the `ExtHostAdvertiseAs` setting.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-host-advertise-as` |
| YAML                 | `ExtHostAdvertiseAs` |
| Environment variable | `EVENTSTORE_EXT_HOST_ADVERTISE_AS` | 

## TCP translations

Both internal and external TCP ports can be advertised using custom values:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-port-advertise-as` |
| YAML                 | `IntTcpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_TCP_PORT_ADVERTISE_AS` | 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-tcp-port-advertise-as` |
| YAML                 | `ExtTcpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT_ADVERTISE_AS` | 

If you want to change how the node TCP address is advertised internally, use the  `IntHostAdvertiseAs` setting. You can use an IP address or a hostname.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-host-advertise-as` |
| YAML                 | `IntHostAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_HOST_ADVERTISE_AS` | 

Externally, TCP is advertised using the address specified in the `ExtIp` or `ExtHostAdvertiseAs` (as for HTTP).

## Advertise to clients

In some cases, the cluster needs to advertise itself to clients using a completely different set of addresses and ports. Usually, you need to do it because addresses and ports configured for the HTTP protocol are not available as-is to the outside world. One of the examples is running a cluster in Docker Compose. In such environment, HTTP uses internal hostnames in the Docker network, which isn't accessible on the host. So, in order to connect to the cluster from the host machine, you need to use `localhost` and translated HTTP ports to reach the cluster nodes.

To configure how the cluster nodes advertise to clients, use the `Advertise<*>ToClient` settings listed below.

Specify the advertised hostname or IP address:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--advertise-host-to-client-as` |
| YAML                 | `AdvertiseHostToClientAs` |
| Environment variable | `EVENTSTORE_ADVERTISE_HOST_TO_CLIENT_AS` | 

Specify the advertised HTTP(S) port:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--advertise-http-port-to-client-as` |
| YAML                 | `AdvertiseHttpPortToClientAs` |
| Environment variable | `EVENTSTORE_ADVERTISE_HTTP_PORT_TO_CLIENT_AS` | 

Specify the advertised TCP port (only if external TCP is enabled):

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--advertise-tcp-port-to-client-as` |
| YAML                 | `AdvertiseTcpPortToClientAs` |
| Environment variable | `EVENTSTORE_ADVERTISE_TCP_PORT_TO_CLIENT_AS` | 

