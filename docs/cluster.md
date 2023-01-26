---
title: Clustering
---

## Highly-available cluster

High availability EventStoreDB allows you to run more than one node as a cluster. Follow this guide to learn how to set up a highly-available EventStoreDB cluster.

### Cluster nodes

EventStoreDB clusters follow a "shared nothing" philosophy, meaning that clustering requires no shared disks for clustering to work. Instead, several database nodes store your data to ensure it isn't lost in case of a drive failure or a node crashing. 

::: tip
Lean more about [cluster node roles](#cluster-node-roles).
:::

EventStoreDB uses a quorum-based replication model, in which a majority of nodes in the cluster must acknowledge that they committed a write to disk before acknowledging the write to the client. This means that to be able to tolerate the failure of _n_ nodes, the cluster must be of size _(2n + 1)_. A three-database-node cluster can continue to accept writes if one node is unavailable. A five-database-node cluster can continue to accept writes if two nodes are unavailable, and so forth.

A typical deployment topology consists of three physical machines, each running one manager node and one database node. Each of the physical machines may have two network interfaces, one for communicating with other cluster members, and one for serving clients. Although it may be preferable in some situations to run over two separate networks. It's also possible to use different TCP ports on one interface. Read more about the network interfaces configuration in the [networking](networking.md) documentation. 

### Cluster size

For any cluster configuration, you first need to decide how many nodes you want, provision each node and set the cluster size option on each node.

The cluster size is a pre-defined value. The cluster expects the number of nodes to match this predefined number, otherwise the cluster would be incomplete and therefore unhealthy.

The cluster cannot be dynamically scaled. If you need to change the number of cluster nodes, the cluster size setting must be changed on all nodes before the new node can join.

Use the `ClusterSize` option to tell each cluster node about how many nodes the cluster should have.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--cluster-size`          |
| YAML                 | `ClusterSize`             |
| Environment variable | `EVENTSTORE_CLUSTER_SIZE` |

**Default**: `1` (single node, no high-availability).

When setting up a cluster, you generally want an odd number of nodes as EventStoreDB uses a quorum based algorithm to handle high availability. We recommended you define an odd number of nodes to avoid split brain problems.

Common values for the `ClusterSize` setting are three or five (to have a majority of two nodes and a majority of three nodes).

### Discovering cluster members

Cluster nodes use the gossip protocol to discover each other and select the cluster leader. There could be only one leader and each client application connecting to the cluster would always be directed to the leader node. 

Cluster nodes need to know about one another to gossip. To start this process, you provide gossip seeds for each node. 

Configure cluster nodes to discover other nodes in one of two ways:
- [via a DNS entry](#cluster-with-dns) and a well-known [gossip port](#gossip-port)
- [via a list of addresses](#cluster-with-gossip-seeds) of other cluster nodes

The multi-address DNS name cluster discovery only works for clusters that use certificates signed by a private certificate authority, or run insecure. For other scenarios you need to provide the gossip seed using hostnames of other cluster nodes.

### Internal communication

When setting up a cluster the nodes must be able to reach each other over both the HTTP channel, and the internal TCP channel. You should ensure that these ports are open on firewalls on the machines and between the machines.

Learn more about [internal TCP configuration](networking.md#tcp-configuration) and [HTTP configuration](networking.md#http-configuration) to set up the cluster properly.

## Cluster with DNS

When you tell EventStoreDB to use DNS for its gossip, the server will resolve the DNS name to a list of IP addresses and connect to each of those addresses to find other nodes. This method is very flexible because you can change the list of nodes on your DNS server without changing the cluster configuration. The DNS method is also useful in automated deployment scenario when you control both the cluster deployment and the DNS server from your infrastructure-as-code scripts.

To use the DNS discovery, you need to set the `ClusterDns` option to the DNS name that allows making an HTTP call to it. When the server starts, it will attempt to make a gRPC call using the `https://<cluster-dns>:<gossip-port>` URL (`http` if the cluster is insecure).

When using a certificate signed by a publicly trusted CA, you'd normally use the wildcard certificate. Ensure that the cluster DNS name fits the wildcard, otherwise the request will fail on SSL check.

When using certificates signed by a private CA, the cluster DNS name must be included to the certificate of each node as SAN (subject alternative name).

You also need to have the `DiscoverViaDns` option to be set to `true` but it is its default value.

| Format               | Syntax                   |
|:---------------------|:-------------------------|
| Command line         | `--cluster-dns`          |
| YAML                 | `ClusterDns`             |
| Environment variable | `EVENTSTORE_CLUSTER_DNS` |

**Default**: `fake.dns`, which doesn't resolve to anything. You have to set it to a proper DNS name when used in combination to the DNS discovery (next setting).

| Format               | Syntax                        |
|:---------------------|:------------------------------|
| Command line         | `--discover-via-dns`          |
| YAML                 | `DiscoverViaDns`              |
| Environment variable | `EVENTSTORE_DISCOVER_VIA_DNS` |

**Default**: `true`, the DNS discovery is enabled by default.

It will be used only if the cluster has more than one node. You must set the `ClusterDns` setting to a proper DNS name.

When using DNS for cluster gossip, you might need to set the `GossipPort` setting to the HTTP port if the external HTTP port setting is not set to `2113` default port. Refer to [gossip port](#gossip-port) option documentation to learn more.

## Cluster with gossip seeds

If you don't want or cannot use the DNS-based configuration, it is possible to tell cluster nodes to call other nodes using their IP addresses. This method is a bit more cumbersome, because each node has to have the list of addresses for other nodes configured, but not its own address.

The setting accepts a comma-separated list of IP addresses or host names with their gossip port values.

| Format               | Syntax                   |
|:---------------------|:-------------------------|
| Command line         | `--gossip-seed`          |
| YAML                 | `GossipSeed`             |
| Environment variable | `EVENTSTORE_GOSSIP_SEED` |

## Gossip protocol

EventStoreDB uses a quorum-based replication model. When working normally, a cluster has one database node known as a leader, and the remaining nodes are followers. The leader node is responsible for coordinating writes while it is the leader. Database nodes use a consensus algorithm to determine which database node should be master and which should be followers. EventStoreDB bases the decision as to which node should be the leader on a number of factors.

For a cluster node to have this information available to them, the nodes gossip with other nodes in the cluster. Gossip runs over HTTP interfaces of cluster nodes.

The gossip protocol configuration can be changed using settings listed below. Pay attention to the settings related to time, like intervals and timeouts, when running in a cloud environment.

### Gossip port

The gossip port is used for constructing the URL for making a gossip request to other nodes that are discovered via DNS. It's not used when using gossip seeds, because in that case the list contains ip addresses and the port.

::: warning
Normally, the cluster gossip port is the same as the HTTP port, so you don't need to change this setting.
:::

| Format               | Syntax                           |
|:---------------------|:---------------------------------|
| Command line         | `--cluster-gossip-port`          |
| YAML                 | `ClusterGossipPort`              |
| Environment variable | `EVENTSTORE_CLUSTER_GOSSIP_PORT` |

**Default**: HTTP port

### Gossip interval

Cluster nodes try to ensure that the communication with their neighbour nodes isn't broken. They use gossip protocol and call each other after a specified period of time. This period is called the gossip interval. You can change the `GossipInvervalMs` setting so cluster nodes check in with each other more or less frequently.

The default value is one second. For cloud deployments, we recommend using two seconds instead (2000 ms).

| Format               | Syntax                          |
|:---------------------|:--------------------------------|
| Command line         | `--gossip-interval-ms`          |
| YAML                 | `GossipIntervalMs`              |
| Environment variable | `EVENTSTORE_GOSSIP_INTERVAL_MS` |

**Default**: `2000` (in milliseconds), which is two seconds.

### Time difference toleration

EventStoreDB expects the time on cluster nodes to be in sync. It is however possible that nodes get their clock desynchronized by a small value. This settings allows adjusting the tolerance of how much the clock on different nodes might be out of sync.

If different nodes have their clock out of sync for a number of milliseconds that exceeds the value of this setting, the gossip gets rejected and the node won't be accepted as the cluster member.

| Format               | Syntax                                    |
|:---------------------|:------------------------------------------|
| Command line         | `--gossip-allowed-difference-ms`          |
| YAML                 | `GossipAllowedDifferenceMs`               |
| Environment variable | `EVENTSTORE_GOSSIP_ALLOWED_DIFFERENCE_MS` |

**Default**: `60000` (in milliseconds), which is one minute.

### Gossip timeout

When nodes call each other using gossip protocol to understand the cluster status, a busy node might delay the response. When a node isn't getting a response from another node, it might consider that other node as dead. Such a situation might trigger the election process.

If your cluster network is congested, you might increase the gossip timeout using the `GossipTimeoutMs` setting, so nodes will be more tolerant to delayed gossip responses. The default value is 2.5 seconds (2500 ms).

| Format               | Syntax                         |
|:---------------------|:-------------------------------|
| Command line         | `--gossip-timeout-ms`          |
| YAML                 | `GossipTimeoutMs`              |
| Environment variable | `EVENTSTORE_GOSSIP_TIMEOUT_MS` |

**Default**: `2500` (in milliseconds).

### Gossip on single node

You can connect using gossip seeds regardless of whether you have a cluster or not. In the previous versions of EventStoreDB gossip on a single node was disabled. Starting from 21.2 it's enabled by default.

::: warning
Please note that the `GossipOnSingleNode` option is deprecated and will be removed in a future version. The gossip endpoint is now unconditionally available for any deployment topology.
:::

| Format               | Syntax                             |
|:---------------------|:-----------------------------------|
| Command line         | `--gossip-on-single-node`          |
| YAML                 | `GossipOnSingleNode`               |
| Environment variable | `EVENTSTORE_GOSSIP_ON_SINGLE_NODE` |

**Default**: `true`

### Leader election timeout

The leader elections are separate to the node gossip, and are used to elect a node as Leader and assign roles to the other nodes.

In some cases the leader election messages may be delayed, which can result in elections taking longer than they should. If you start seeing election timeouts in the logs or if you've needed to increase the gossip timeout due to a congested network, then you should consider increasing the leader election timeout as well.

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--leader-election-timeout-ms`          |
| YAML                 | `LeaderElectionTimeoutMs`               |
| Environment variable | `EVENTSTORE_LEADER_ELECTION_TIMEOUT_MS` |

**Default**: `1000` (in milliseconds).

## Cluster node roles

Every node in an EventStoreDB cluster can have one of three roles: Leader, Follower and ReadOnlyReplica.

All writes are executed by the Leader node unconditionally, confirmed by a number of other nodes, described on the [acknowledgement](#acknowledgements) page. Subscription clients can connect to Follower or Clone nodes to offload reads from the Leader node.

### Leader

A cluster assigns the leader role based on an election process. The node with the leader role ensures that the data are committed and persisted to disk before sending back to the client an acknowledge message. A cluster can only have one leader at a time. If a cluster detects two nodes with a leader role, a new election begins and shuts down the node with less data to restart and re-join the cluster.

### Follower

A cluster assigns the follower role based on an election process. A cluster uses one or more nodes with the follower role to form the quorum, or the majority of nodes necessary to confirm that the write is persisted.

### Read-only replica

You can add read-only replica nodes, which won't become cluster members and will not take part in elections. Read-only replicas can be used for scaling up reads if you have many catch-up subscriptions and want to off-load cluster members.

A cluster asynchronously replicates data one way to a node with the read-only replica role. You don't need to wait for an acknowledgement message as the node is not part of the quorum. For this reason a node with a read-only replica role does not add much overhead to the other nodes.

You need to explicitly configure the node as a read-only replica using this setting:

| Format               | Syntax                         |
|:---------------------|:-------------------------------|
| Command line         | `--read-only-replica`          |
| YAML                 | `ReadOnlyReplica`              |
| Environment variable | `EVENTSTORE_READ_ONLY_REPLICA` |

**Default**: `false`, set to `true` for a read-only replica node.

The replica node needs to have the cluster gossip DNS or seed configured. For the gossip seed, use DNS names or IP addresses of all other cluster nodes, except read-only replicas.

### Node priority

You can control which clones the cluster promotes with the `NodePriority` setting. The default value is `0`, and the cluster is more likely to promote clones with higher values.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--node-priority`         |
| YAML                 | `NodePriority`            |
| Environment variable | `EVENTSTORE_NODE_PRORITY` |

**Default**: `0`.

::: tip
Changing `NodePriority` doesn't guarantee that the cluster won't promote the clone. It's only one of the criteria that the Election Service considers.
:::

## Acknowledgements

By default, every write to the cluster needs to be acknowledged by all cluster members. This condition could be relaxed to speed up the writes, but it comes with a risk of data loss.

We do not advise to change the acknowledgement settings.

| Format               | Syntax                    |
|:---------------------|:--------------------------|
| Command line         | `--commit-count`          |
| YAML                 | `CommitCount`             |
| Environment variable | `EVENTSTORE_COMMIT_COUNT` |

**Default**: `-1`, all nodes must acknowledge commits.

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--prepare-count`          |
| YAML                 | `PrepareCount`             |
| Environment variable | `EVENTSTORE_PREPARE_COUNT` |

**Default**: `-1`, all nodes must acknowledge prepares.
