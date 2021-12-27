# Highly-available cluster

High availability EventStoreDB allows you to run more than one node as a cluster. Follow this guide to learn how to set up a highly-available EventStoreDB cluster.

## Cluster nodes

EventStoreDB clusters follow a "shared nothing" philosophy, meaning that clustering requires no shared disks for clustering to work. Instead, several database nodes store your data to ensure it isn't lost in case of a drive failure or a node crashing. 

::: tip
Lean more about [cluster node roles](./node-roles.md).
:::

EventStoreDB uses a quorum-based replication model, in which a majority of nodes in the cluster must acknowledge that they committed a write to disk before acknowledging the write to the client. This means that to be able to tolerate the failure of _n_ nodes, the cluster must be of size _(2n + 1)_. A three-database-node cluster can continue to accept writes if one node is unavailable. A five-database-node cluster can continue to accept writes if two nodes are unavailable, and so forth.

A typical deployment topology consists of three physical machines, each running one manager node and one database node. Each of the physical machines may have two network interfaces, one for communicating with other cluster members, and one for serving clients. Although it may be preferable in some situations to run over two separate networks. It's also possible to use different TCP ports on one interface. Read more about the network interfaces configuration in the [networking](../networking/) documentation. 

## Cluster size

For any cluster configuration, you first need to decide how many nodes you want, provision each node and set the cluster size option on each node.

The cluster size is a pre-defined value. The cluster expects the number of nodes to match this predefined number, otherwise the cluster would be incomplete and therefore unhealthy.

The cluster cannot be dynamically scaled. If you need to change the number of cluster nodes, the cluster size setting must be changed on all nodes before the new node can join.

Use the `ClusterSize` option to tell each cluster node about how many nodes the cluster should have.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--cluster-size` |
| YAML                 | `ClusterSize` |
| Environment variable | `EVENTSTORE_CLUSTER_SIZE` |

**Default**: `1` (single node, no high-availability).

When setting up a cluster, you generally want an odd number of nodes as EventStoreDB uses a quorum based algorithm to handle high availability. We recommended you define an odd number of nodes to avoid split brain problems.

Common values for the `ClusterSize` setting are three or five (to have a majority of two nodes and a majority of three nodes).

## Discovering cluster members

Cluster nodes use the gossip protocol to discover each other and select the cluster leader. There could be only one leader and each client application connecting to the cluster would always be directed to the leader node. 

Cluster nodes need to know about one another to gossip. To start this process, you provide gossip seeds for each node. 

Configure cluster nodes to discover other nodes in one of two ways:
- [via a DNS entry](./using-dns.md) and a well-known [gossip port](./gossip.md#gossip-port)
- [via a list of addresses](./using-ip-addresses.md) of other cluster nodes

The multi-address DNS name cluster discovery only works for clusters that use certificates signed by a private certificate authority, or run insecure. For other scenarios you need to provide the gossip seed using hostnames of other cluster nodes.

## Internal communication

When setting up a cluster the nodes must be able to reach each other over both the HTTP channel, and the internal TCP channel. You should ensure that these ports are open on firewalls on the machines and between the machines.

Learn more about [internal TCP configuration](../networking/tcp.md#internal) and [HTTP configuration](../networking/http.md) to set up the cluster properly.


