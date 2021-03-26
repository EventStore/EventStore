# NAT and port forward

## Override the address

Due to NAT (network address translation), or other reasons a node may not be bound to the address it is reachable from other nodes. For example, the machine has an IP address of `192.168.1.13`, but the node is visible to other nodes as `10.114.12.112`.

Options described below allow you to tell the node that even though it is bound to a given address it should not gossip that address. When returning links over HTTP, EventStoreDB will also use the specified addresses instead of physical addresses, so the clients that use HTTP can follow those links.

Another case when you might want to specify the advertised address although there's no address translation involved. When you configure EventStoreDB to bind to `0.0.0.0`, it will use the first non-loopback address for gossip. It might or might not be the address you want it to use for internal communication. Whilst the best way to avoid such a situation is to [configure the binding](./internal.md#interface) properly, you can also use the `IntIpAdvertiseAs` setting with the correct internal IP address.

The only place where these settings make any effect is the [gossip](../clustering/gossip.md) endpoint response. Cluster nodes gossip over the internal HTTP and there all the `Int<*>AdvertiseAs` will apply there, if specified. Clients use the external HTTP endpoint for gossip and if any `Ext<*>AdvertiseAs` setting is specified, EventStoreDB will override addresses and ports in the gossip response accordingly.

Two settings below determine of the node IP address needs to be replaced.

Replace the internal interface IP address:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-ip-advertise-as` |
| YAML                 | `IntIpAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_IP_ADVERTISE_AS` | 

Replace the external interface IP address:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-ip-advertise-as` |
| YAML                 | `ExtIpAdvertiseAs` |
| Environment variable | `EVENTSTORE_EXT_IP_ADVERTISE_AS` | 

If your setup involves not only translating IP addresses, but also changes ports using port forwarding, you would need to change how the ports are advertised too.

When any of those settings are set to zero (default), EventStoreDB won't override the port and will use the configured port when advertising itself.

Replace the external HTTP port:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-http-port-advertise-as` |
| YAML                 | `ExtHttpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_EXT_HTTP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the internal HTTP port:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-http-port-advertise-as` |
| YAML                 | `IntHttpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_HTTP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the external TCP port:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ExtTcpPortAdvertiseAs` |
| YAML                 | `ext-tcp-port-advertise-as` |
| Environment variable | `EVENTSTORE_EXT_TCP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)

Replace the internal TCP port:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-port-advertise-as` |
| YAML                 | `IntTcpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_TCP_PORT_ADVERTISE_AS` | 

**Default**: `0` (n/a)
