---
title: "Upgrade Guide"
---

## Upgrade guide for EventStoreDB 23.10

Packages are available on our [website](https://www.eventstore.com/downloads) and can be installed via [Packagecloud](https://packagecloud.io/EventStore/EventStore-OSS), [Chocolatey](https://chocolatey.org/packages/eventstore-oss), or [Docker](https://hub.docker.com/r/eventstore/eventstore/tags?page=1&name=23.10). See detailed commands/instructions for each platform.

### Should I upgrade?

As a Long-Term Support (LTS) release, we will support 23.10 for 2 years, concluding with the 25.10.0 release in October 2025.

- Users on versions 23.6.0 should upgrade as support for this version has ceased.
- Users on versions 22.10.x should update to at least 22.10.3 or to 23.10 to use new features.
- Users on version 21.10.x or earlier versions should upgrade directly to 23.10 to maintain support.

### Upgrade procedure

An online rolling upgrade can be done from these versions of EventStoreDB, directly to 23.10:
- 23.6
- 22.10
- 21.10

Follow the upgrade procedure below on each node, starting with a follower node:

1. Stop the node.
2. Upgrade EventstoreDB to 23.10 and update configuration.
3. Start the node.
4. Wait for the node to become a follower or read-only replica.
5. Repeat on the next node. 

As illustrated below:

::: card
![EventStoreDB upgrade procedure for each node](./images/upgrade-procedure.png)
:::


Upgrading the cluster in this manner keeps the cluster online and able to service requests. There may still be disruptions to your services during the upgrade, namely:
- Client connections may be disconnected when nodes go offline, or when elections take place.
- The cluster is less fault tolerant while a node is offline for an upgrade because the cluster requires a quorum of nodes to be online to service write requests.
- Replicating large amounts of data to a node can have a performance impact on the Leader in the cluster.

### Breaking changes from version 22.10 and earlier

The updates to anonymous access described in the [release notes](https://www.eventstore.com/blog/23.10.0-release-notes) have introduced some breaking changes. We have also removed, renamed, and deprecated some options in EventStoreDB.

None of these changes will prevent you from performing an online rolling upgrade of the cluster, but you will need to take them into account before you perform an upgrade.

When upgrading from 22.10 and earlier, you will need to account for the following breaking changes:

#### Clients must be authenticated by default

We have disabled anonymous access to streams by default in this version. This means that read and write requests from clients need to be authenticated.

If you see authentication errors when connecting to EventStoreDB after upgrading, please ensure that you are either using default credentials on the connection, or are providing user credentials with the request itself.

If you want to revert back to the old behavior, you can enable the `AllowAnonymousStreamAccess` and `AllowAnonymousEndpointAccess` options in EventStoreDB.
Requests to the HTTP API must be authenticated by default.
Like with anonymous access to streams, anonymous access to the HTTP and gRPC endpoints has been disabled by default. The exceptions are the `/gossip`, `/info`, and `/ping` endpoints.

Any tools or monitoring scripts accessing the HTTP endpoints (e.g. `/stats`) will need to make authenticated requests to EventStoreDB.

If you want to revert back to the old behavior, you can enable the `AllowAnonymousStreamAccess` and `AllowAnonymousEndpointAccess` options in EventStoreDB.

#### PrepareCount and CommitCount options have been removed

We have removed the `PrepareCount` and `CommitCount` options from EventStoreDB. EventStoreDB will now fail if these options are present in the config on startup.

These options did not have any effect and can be safely removed from your configuration file if you have them defined.

#### Persistent Subscriptions Config event has been renamed

We have renamed the event type used to store a persistent subscriptions configuration from `PersistentConfig1` to `$PersistentConfig`. This event type is a system event, so naming it as such will allow certain filters to exclude it correctly.

If you have any tools or clients relying on this event type, then you will need to update them before you upgrade.

### Breaking changes from 21.10 and earlier

If you are upgrading from version 21.10 and earlier, then you need to be aware of a breaking change in the TCP proto:

#### Proto2 upgraded to Proto3 (TCP)

The server now uses Proto3 for messages sent over TCP. This affects both replication and the legacy dotnet client.

EventStoreDB nodes on version 22.10 cannot replicate data to version 21.10 and below, but older nodes can still replicate to version 22.10 and above.
Follow the [upgrade procedure](#upgrade-procedure) and ensure that the Leader node is the last node to be upgraded to avoid any issues.

### Deprecated configuration options

Several options are deprecated and slated for removal in future releases. See the table below for guidance.

| Deprecated Option | Use Instead |
|:------------------|:-------------|
| ExtIp | NodeIp |
| ExtPort | NodePort |
| HttpPortAdvertiseAs | NodePortAdvertiseAs |
| ExtHostAdvertiseAs | NodeHostAdvertiseAs |
| AdvertiseHttpPortToClientAs | AdvertiseNodePortToClientAs |
| IntIp | ReplicationIp |
| IntTcpPort | ReplicationTcpPort |
| IntTcpPortAdvertiseAs | ReplicationTcpPortAdvertiseAs |
| IntHostAdvertiseAs | ReplicationHostAdvertiseAs |
| IntTcpHeartbeatTimeout | ReplicationHeartbeatTimeout |
| IntTcpHeartbeatInterval | ReplicationHeartbeatInterval |
