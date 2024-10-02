---
title: "Upgrade guide"
order: 5
---

# Upgrade guide for EventStoreDB 24.10

As of version 24.10.0, all of our packages are hosted on [Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-preview/packages/). Packages are available for [Debian](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-deb), [RedHat](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-rpm), [Docker](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-docker), and [NuGet](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-nuget).

You can also download the package files for each platform from our [website](https://www.eventstore.com/downloads).

If you have a previous version of EventStoreDB installed through PackageCloud or Chocolatey, please uninstall those versions before installing version 24.10 from Cloudsmith.

### Should you upgrade?

Version 24.10 is currently in preview and running it in production is not supported.

We recommend trying the 24.10 preview if you are interested in any of the [new features](./whatsnew.md) coming in the official 24.10 LTS release.

### Upgrade procedure

You can perform an online rolling upgrade directly to 24.6 from these versions of EventStoreDB:
- 24.2
- 23.10
- 22.10
- 21.10

Follow the upgrade procedure below on each node, starting with a follower node:

1. Stop the node.
2. Uninstall any previous versions of EventStoreDB.
3. Install EventStoreDB 24.10 and update configuration. If you are using any licensed features, ensure that you configure a [license key](../configuration/license-keys.md).
4. Start the node.
5. Wait for the node to become a follower or read-only replica.
6. Repeat the process for the next node.

Upgrading the cluster this way keeps the cluster online and able to service requests. There may still be disruptions to your services during the upgrade, namely:
- Client connections may be disconnected when nodes go offline, or when elections take place.
- The cluster is less fault-tolerant while a node is offline for an upgrade because the cluster requires a quorum of nodes to be online to service write requests.
- Replicating large amounts of data to a node can have a performance impact on the Leader in the cluster.

::: warning
If you modified the Linux service file to increase the open files limit, those changes will be overridden during the upgrade. You will need to reapply them after the upgrade.
:::

### Breaking changes

#### From version 24.6 and earlier

##### Histograms endpoint has been removed

The `/histogram/{name}` endpoint has been removed.

Any tooling that relies on the histogram endpoint will receive a 404 after when requesting this endpoint after upgrading.

##### Support for v1 PTables has been removed

Support for extremely old PTables (v1) has been removed.

This will only affect databases that were created on EventStoreDB versions 3.9.0 and before, and which have not upgraded their PTables since EventStoreDB version 3.9.0.

PTables are automatically upgraded when they are merged, or when the PTables are rebuilt. So if your EventStoreDB has been running for some time on a version greater than 3.9.0, then you are unlikely to be affected by this change.

If 32bit PTables are present we detect them on startup and exit. If this happens, you can use a version between v3.9.0 and v24.10.0 to upgrade the PTables, or rebuild the index.

#### From version 23.10 and earlier

##### External TCP API removed

The external TCP API was removed in 24.2.0. This affects external clients using the TCP API and configurations related to it.

::: tip
EventStoreDB 24.10 includes [a plugin](../configuration/networking.md#external-tcp) that enables the TCP client protocol. This plugin can only be used with a [license](../configuration/license-keys.md)
:::

A number of configuration options have been removed as part of this. EventStoreDB will not start by default if any of the following options are present in the database configuration:

- `AdvertiseTcpPortToClientAs`
- `DisableExternalTcpTls`
- `EnableExternalTcp`
- `ExtHostAdvertiseAs`
- `ExtTcpHeartbeatInterval`
- `ExtTcpHeartbeatTimeout`
- `ExtTcpPort`
- `ExtTcpPortAdvertiseAs`
- `NodeHeartbeatInterval`
- `NodeHeartbeatTimeout`
- `NodeTcpPort`
- `NodeTcpPortAdvertiseAs`

#### From version 22.10 and earlier

The updates to anonymous access described in the [release notes](https://www.eventstore.com/blog/23.10.0-release-notes) have introduced some breaking changes. We have also removed, renamed, and deprecated some options in EventStoreDB.

None of these changes will prevent you from performing an online rolling upgrade of the cluster, but you will need to take them into account before you perform an upgrade.

When upgrading from 22.10 and earlier, you will need to account for the following breaking changes:

##### Clients must be authenticated by default

We have disabled anonymous access to streams by default in this version. This means that read and write requests from clients need to be authenticated.

If you see authentication errors when connecting to EventStoreDB after upgrading, please ensure that you are either using default credentials on the connection, or are providing user credentials with the request itself.

If you want to revert back to the old behavior, you can enable the `AllowAnonymousStreamAccess` and `AllowAnonymousEndpointAccess` options in EventStoreDB.
Requests to the HTTP API must be authenticated by default.
Like with anonymous access to streams, anonymous access to the HTTP and gRPC endpoints has been disabled by default. The exceptions are the `/gossip`, `/info`, and `/ping` endpoints.

Any tools or monitoring scripts accessing the HTTP endpoints (e.g. `/stats`) will need to make authenticated requests to EventStoreDB.

If you want to revert back to the old behavior, you can enable the `AllowAnonymousStreamAccess` and `AllowAnonymousEndpointAccess` options in EventStoreDB.

##### PrepareCount and CommitCount options have been removed

We have removed the `PrepareCount` and `CommitCount` options from EventStoreDB. EventStoreDB will now fail if these options are present in the config on startup.

These options did not have any effect and can be safely removed from your configuration file if you have them defined.

##### Persistent subscriptions config event has been renamed

We have renamed the event type used to store a persistent subscriptions configuration from `PersistentConfig1` to `$PersistentConfig`. This event type is a system event, so naming it as such will allow certain filters to exclude it correctly.

If you have any tools or clients relying on this event type, then you will need to update them before you upgrade.

#### From 21.10 and earlier

If you are upgrading from version 21.10 and earlier, then you need to be aware of a breaking change in the TCP proto:

##### Proto2 upgraded to Proto3 (TCP)

The server now uses Proto3 for messages sent over TCP. This affects replication between servers in a cluster.

EventStoreDB nodes on version 22.10 cannot replicate data to version 21.10 and below, but older nodes can still replicate to version 22.10 and above.
Follow the [upgrade procedure](#upgrade-procedure) and ensure that the Leader node is the last node to be upgraded to avoid any issues.

##### Deprecated configuration options

Several options are deprecated and slated for removal in future releases. See the table below for guidance.

| Deprecated Option             | Use Instead                     |
|:------------------------------|:--------------------------------|
| `ExtIp`                       | `NodeIp`                        |
| `ExtPort`                     | `NodePort`                      |
| `HttpPortAdvertiseAs`         | `NodePortAdvertiseAs`           |
| `ExtHostAdvertiseAs`          | `NodeHostAdvertiseAs`           |
| `AdvertiseHttpPortToClientAs` | `AdvertiseNodePortToClientAs`   |
| `IntIp`                       | `ReplicationIp`                 |
| `IntTcpPort`                  | `ReplicationTcpPort`            |
| `IntTcpPortAdvertiseAs`       | `ReplicationTcpPortAdvertiseAs` |
| `IntHostAdvertiseAs`          | `ReplicationHostAdvertiseAs`    |
| `IntTcpHeartbeatTimeout`      | `ReplicationHeartbeatTimeout`   |
| `IntTcpHeartbeatInterval`     | `ReplicationHeartbeatInterval`  |
