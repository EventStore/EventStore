# Network configuration

EventStoreDB provides two interfaces: proprietary TCP and REST-contract-based HTTP. Nodes in the cluster communicate with each other using the TCP protocol, but use HTTP for [discovering other cluster nodes](../clustering/README.md#discovering-cluster-members).

Server nodes separate internal and external communication explicitly. All the communication between the cluster nodes is considered internal and all the clients that connect to the database are external. EventStoreDB allows you to separate network configurations for internal and external communication. For example, you can use different network interfaces on the node. All the internal communication can go over the isolated private network but access for external clients can be configured on another interface, which is connected to a more open network. You can set restrictions on those external interfaces to make your deployment more secure.

There are multiple options that allow configuring TCP and HTTP protocols, internally and externally, on the server.

## External communication

Configure the [external network settings](./external.md) to allow clients to connect to your instance.

## Cluster communication

If you run EventStoreDB as a cluster, you need to configure the [internal communication](./internal.md) so cluster nodes can talk to each other.





