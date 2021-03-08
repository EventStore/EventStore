# Cluster node roles

Every node in an EventStoreDB cluster can have one of three roles: Leader, Follower and ReadOnlyReplica. 

All writes are executed by the Leader node unconditionally, confirmed by a number of other nodes, described on the [acknowledgement](./acknowledgements.md) page. Subscription clients can connect to Follower or Clone nodes to offload reads from the Leader node.

## Leader

A cluster assigns the leader role based on an election process. The node with the leader role ensures that the data are committed and persisted to disk before sending back to the client an acknowledge message. A cluster can only have one leader at a time. If a cluster detects two nodes with a leader role, a new election begins and shuts down the node with less data to restart and re-join the cluster.

## Follower

A cluster assigns the follower role based on an election process. A cluster uses one or more nodes with the follower role to form the quorum, or the majority of nodes necessary to confirm that a write is persisted.

## Read-only replica

You can add read-only replica nodes, which won't become cluster members and will not take part in elections. Read-only replicas can be used for scaling up reads if you have many catch-up subscriptions and want to off-load cluster members.

A cluster asynchronously replicates data one way to a node with the read-only replica role. You don't need to wait for an acknowledgement message as the node is not part of the quorum. For this reason a node with a read-only replica role does not add much overhead to the other nodes.

You need to explicitly configure the node as a read-only replica using this setting:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--read-only-replica` |
| YAML                 | `ReadOnlyReplica` |
| Environment variable | `EVENTSTORE_READ_ONLY_REPLICA` |

**Default**: `false`, set to `true` for a read-only replica node.

The replica node needs to have the cluster gossip DNS or seed configured. For the gossip seed, use DNS names or IP addresses of all other cluster nodes, except read-only replicas.

## Node priority

You can control which clones the cluster promotes with the `NodePriority` setting. The default value is `0`, and the cluster is more likely to promote clones with higher values.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--node-priority` |
| YAML                 | `NodePriority` |
| Environment variable | `EVENTSTORE_NODE_PRORITY` |

**Default**: `0`.

::: tip
Changing `NodePriority` doesn't guarantee that the cluster won't promote the clone. It's only one of the criteria that the Election Service considers.
:::
