# Network configuration

EventStoreDB provides two interfaces: 
- HTTP(S) for gRPC communication and REST APIs
- TCP for cluster replication (internal) and legacy clients (external) 

Nodes in the cluster replicate with each other using the TCP protocol, but use gRPC for [discovering other cluster nodes](../clustering/README.md#discovering-cluster-members).

Server nodes separate internal and external TCP communication explicitly, but use a single HTTP binding. Replication between the cluster nodes is considered internal and all the TCP clients that connect to the database are external. EventStoreDB allows you to separate network configurations for internal and external communication. For example, you can use different network interfaces on the node. All the internal communication can go over the isolated private network but access for external clients can be configured on another interface, which is connected to a more open network. You can set restrictions on those external interfaces to make your deployment more secure.

For gRPC and HTTP, there's no internal vs external separation of traffic.

## HTTP configuration

Find out more about configuring the HTTP protocol on the [HTTP configuration](./http.md) page.

## TCP configuration

Find out more about configuring the TCP protocol on the [TCP configuration](./tcp.md) page.

## Address translation

If your network environment exposes EventStoreDB cluster nodes using translated addresses and ports, you'd need to tell each node how to expose itself in the gossip seed using [Advertise As settings](./nat.md).






