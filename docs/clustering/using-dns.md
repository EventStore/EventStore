# Cluster with DNS

When you tell EventStoreDB to use DNS for its gossip, the server will resolve the DNS name to a list of IP addresses and connect to each of those addresses to find other nodes. This method is very flexible because you can change the list of nodes on your DNS server without changing the cluster configuration. The DNS method is also useful in automated deployment scenario when you control both the cluster deployment and the DNS server from your infrastructure-as-code scripts.

::: warning
Cluster discovery using the `ClusterDns` setting only works for:
- Insecure clusters
- Secure clusters with SSL certificates that contain IP addresses of the nodes

You cannot use `ClusterDns` for gossip when using wildcard certificates as they don't have IP address SANs.
:::

To use the DNS discovery, you need to set the `ClusterDns` option to the DNS name that resolves to a list of IP addresses for the cluster nodes. You also need to have the `DiscoverViaDns` option to be set to `true` but it is its default value.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--cluster-dns` |
| YAML                 | `ClusterDns` |
| Environment variable | `EVENTSTORE_CLUSTER_DNS` |

**Default**: `fake.dns`, which doesn't resolve to anything. You have to set it to a proper DNS name when used in combination to the DNS discovery (next setting).

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--discover-via-dns` |
| YAML                 | `DiscoverViaDns` |
| Environment variable | `EVENTSTORE_DISCOVER_VIA_DNS` |

**Default**: `true`, the DNS discovery is enabled by default. 

It will be used only if the cluster has more than one node. You must set the `ClusterDns` setting to a proper DNS name.

When using DNS for cluster gossip, you might need to set the `GossipPort` setting to the HTTP port if the external HTTP port setting is not set to `2113` default port. Refer to [gossip port](./gossip.md#gossip-port) option documentation to learn more.
