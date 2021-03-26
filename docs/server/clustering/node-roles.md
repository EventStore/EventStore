# Cluster node roles

Every node in an EventStoreDB cluster can have one of three roles: Leader, Follower and Clone. 

All writes are executed by the Leader node unconditionally, confirmed by a number of other nodes, described on the [acknowledgement](./acknowledgements.md) page. Subscription clients can connect to Follower or Clone nodes to offload reads from the Leader node.

## Leader

::: tip
Leader node role was previously known as `Master`.
:::

A cluster assigns the leader role based on an election process. The node with the leader role ensures that the data are committed and persisted to disk before sending back to the client an acknowledge message. A cluster can only have one leader at a time. If a cluster detects two nodes with a leader role, a new election begins and shuts down the node with less data to restart and re-join the cluster.

## Follower

::: tip
Follower role was previously known as `Slave`.
:::

A cluster assigns the follower role based on an election process. A cluster uses one or more nodes with the follower role to form the quorum, or the majority of nodes necessary to confirm that a write is persisted.

## Clone

::: warning
Clone nodes aren't supported in the latest versions of EventStoreDB (20+) and replaced by read-only replicas. The reason for this is that clone nodes can be promoted to cluster members but writes to the cluster never require acknowledgments from clone nodes. Such a collision might lead to a situation of a clone node that hasn't received the latest data to be promoted to a cluster member and potentially cause data loss.
:::

If you add nodes to a cluster beyond the number of nodes specified in the `ClusterSize` setting the cluster automatically assigns them the clone role.

A cluster asynchronously replicates data one way to a node with the clone role. You don't need to wait for an acknowledgement message as the node is not part of the quorum. For this reason a node with a clone role does not add much overhead to the other nodes.

If a cluster loses nodes to take it below `ClusterSize`, then the cluster can promote a clone to a leader or follower role.

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
