# Clustering example

<!-- TODO: Needs Linux instructions -->

This document covers setting up EventStoreDB with only database nodes.

::: tip Next steps
[Read here](node-roles.md) for more information on the roles available for nodes in an EventStoreDB cluster.
:::

## Running on the same machine

To start, set up three nodes running on a single machine. Run each of the commands below in its own console window. You either need admin privileges or have ACLs setup with IIS if running under Windows. Unix-like operating systems need no configuration. Replace "127.0.0.1" with whatever IP address you want to run on.

```powershell
EventStore.ClusterNode.exe --mem-db --log .\logs\log1 --int-ip 127.0.0.1 --ext-ip 127.0.0.1 --int-tcp-port=1111 --ext-tcp-port=1112 --int-http-port=1113 --ext-http-port=1114 --cluster-size=3 --discover-via-dns=false --gossip-seed=127.0.0.1:2113,127.0.0.1:3113
EventStore.ClusterNode.exe --mem-db --log .\logs\log2 --int-ip 127.0.0.1 --ext-ip 127.0.0.1 --int-tcp-port=2111 --ext-tcp-port=2112 --int-http-port=2113 --ext-http-port=2114 --cluster-size=3 --discover-via-dns=false --gossip-seed=127.0.0.1:1113,127.0.0.1:3113
EventStore.ClusterNode.exe --mem-db --log .\logs\log3 --int-ip 127.0.0.1 --ext-ip 127.0.0.1 --int-tcp-port=3111 --ext-tcp-port=3112 --int-http-port=3113 --ext-http-port=3114 --cluster-size=3 --discover-via-dns=false --gossip-seed=127.0.0.1:1113,127.0.0.1:2113
```

You should now have three nodes running together in a cluster. If you kill one of the nodes, it continues running. This binds to the loopback interface. To access EventStoreDB from outside your machine, specify a different IP address for the `--ext-ip` parameter.

## Running on separate machines

### Using gossip seeds

Most important is to understand the "gossip seeds". You are instructing seed locations for when the node first starts and needs to begin gossiping. Any node can be a seed. By giving each node the other nodes you ensure that there is always another node to gossip with, if a quorum can be built. If you want to move this to run on three machines, change the IPs on the command line to something like this:

```powershell
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log1 --int-ip 192.168.0.1 --ext-ip 192.168.0.1 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --discover-via-dns=false --gossip-seed=192.168.0.2:2112,192.168.0.3:2112
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log2 --int-ip 192.168.0.2 --ext-ip 192.168.0.2 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --discover-via-dns=false --gossip-seed=192.168.0.1:2112,192.168.0.3:2112
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log3 --int-ip 192.168.0.3 --ext-ip 192.168.0.3 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --discover-via-dns=false --gossip-seed=192.168.0.1:2112,192.168.0.2:2112
```

### Using DNS

Entering the commands above into each node is tedious and error-prone (especially as the replica set counts change). Another configuration option is to create a DNS entry that points to all the nodes in the cluster and then specify that DNS entry with the appropriate port:

```powershell
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log1 --int-ip 192.168.0.1 --ext-ip 192.168.0.1 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --cluster-dns eventstore.local --cluster-gossip-port=2112
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log2 --int-ip 192.168.0.2 --ext-ip 192.168.0.2 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --cluster-dns eventstore.local --cluster-gossip-port=2112
EventStore.ClusterNode.exe --mem-db --log c:\dbs\cluster\log3 --int-ip 192.168.0.3 --ext-ip 192.168.0.3 --int-tcp-port=1112 --ext-tcp-port=1113 --int-http-port=2112 --ext-http-port=2113 --cluster-size=3 --cluster-dns eventstore.local --cluster-gossip-port=2112
```

::: tip
You can also use the method above for HTTP clients to avoid using a load balancer and fall back to round robin DNS for many deployments.
:::

### Run in docker-compose using DNS

Create a file _docker-compose.yaml_ with following content:

@[code{curl}](../samples/docker-compose.yaml)

Run containers:
```bash
docker-compose up
```

## Internal vs. external networks

You can optionally segregate all EventStoreDB communications to different networks. For example, internal networks for tasks like replication, and external networks for communication between clients. You can place these communications on segregated networks which is often a good idea for both performance and security purposes.

To setup an internal network, use the command line parameters provided above, but prefixed with `int-`. All communications channels also support enabling SSL for the connections.

## HTTP clients

If you want to use the HTTP API, you can send requests to any cluster node as the node will forward the request to the cluster leader. So, you can use the DNS entry for the cluster gossip when connecting to the cluster. However, you need to remember that forwarding requests introduce an additional network call, so there is a chance that a request gets transferred over the network twice: from your client to the follower node and then from the follower node to the leader node.

EventStoreDB exposes an HTTP endpoint that can tell you what node is the leader now, so you might want to make your client smarter and check who is the leader from time to time. Sending requests directly to the leader node will make the communication faster and more reliable.

## Native TCP clients

You can connect to the cluster using the native TCP interface. The client APIs support switching between nodes internally. As such if you have a master failover the connection automatically handle retries on another node.
