# Gossip protocol

EventStoreDB uses a quorum-based replication model. When working normally, a cluster has one database node known as a leader, and the remaining nodes are followers. The leader node is responsible for coordinating writes while it is the leader. Database nodes use a consensus algorithm to determine which database node should be master and which should be followers. EventStoreDB bases the decision as to which node should be the leader on a number of factors.

For a cluster node to have this information available to them, the nodes gossip with other nodes in the cluster. Gossip runs over the internal (and optionally the external) HTTP interfaces of cluster nodes.

The gossip protocol configuration can be changed using settings listed below. Pay attention to the settings related to time, like intervals and timeouts, when running in a cloud environment.

## Gossip port

It's used for constructing the URL for making a gossip request to other nodes that are discovered via DNS. It's not used when using gossip seeds, because in that case the list contains ip addresses and the port.  

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--cluster-gossip-port` |
| YAML                 | `ClusterGossipPort` |
| Environment variable | `EVENTSTORE_CLUSTER_GOSSIP_PORT` |

**Default**: `30777`

## Gossip interval

Cluster nodes try to ensure that the communication with their neighbour nodes isn't broken. They use gossip protocol and call each other after a specified period of time. This period is called the gossip interval. You can change the `GossipInvervalMs` setting so cluster nodes check in with each other more or less frequently.

The default value is one second. For cloud deployments, we recommend using two seconds instead (2000 ms).

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--gossip-interval-ms` |
| YAML                 | `GossipIntervalMs` |
| Environment variable | `EVENTSTORE_GOSSIP_INTERVAL_MS` |

**Default**: `1000` (in milliseconds), which is one second.

## Time difference toleration

EventStoreDB expects the time on cluster nodes to be in sync. It is however possible that nodes get their clock desynchronized by a small value. This settings allows adjusting the tolerance of how much the clock on different nodes might be out of sync.

If different nodes have their clock out of sync for a number of milliseconds that exceeds the value of this setting, the gossip gets rejected and the node won't be accepted as the cluster member.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--gossip-allowed-difference-ms` |
| YAML                 | `GossipAllowedDifferenceMs` |
| Environment variable | `EVENTSTORE_GOSSIP_ALLOWED_DIFFERENCE_MS` |

**Default**: `60000` (in milliseconds), which is one minute.

## Gossip timeout

When nodes call each other using gossip protocol to understand the cluster status, a busy node might delay the response. When a node isn't getting a response from another node, it might consider that other node as dead. Such a situation might trigger the election process.

If your cluster network is congested, you might increase the gossip timeout using the `GossipTimeoutMs` setting, so nodes will be more tolerant to delayed gossip responses. The default value is half a second, but for cloud deployments we recommend setting it to 2.5 seconds (2500 ms).

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--gossip-timeout-ms` |
| YAML                 | `GossipTimeoutMs` |
| Environment variable | `EVENTSTORE_GOSSIP_TIMEOUT_MS` |

**Default**: `500` (in milliseconds).

## Gossip on single node

When you run a single-node instance of EventStoreDB, the gossip communication is unnecessary. However, if your production environment uses a multi-node cluster and the test environment runs on a single node, you might want to keep the connection style consistent. EventStoreDB clients use either a single-node or gossip-style connection. So, to prevent changing the connection style, you might want to connect to your single-node instance using the gossip protocol as well. To do so, you'd need to enable gossip for that instance as it is disabled by default. Use the `GossipOnSingleNode` setting to change this behaviour.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--gossip-on-single-node` |
| YAML                 | `GossipOnSingleNode` |
| Environment variable | `EVENTSTORE_GOSSIP_ON_SINGLE_NODE` |

**Default**: `false`

