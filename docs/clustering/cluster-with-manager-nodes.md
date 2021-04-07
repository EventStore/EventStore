---
commercial: true
title: Cluster with manager nodes
---

# Setting up a cluster with manager nodes

High availability EventStoreDB allows you to run more than one node as a cluster. There are two modes available for clustering:

- With database nodes only (open source and commercial)
- With manager nodes and database nodes (commercial only)

This document covers setting up EventStoreDB with manager nodes and database nodes.

When setting up a cluster, you generally want an odd number of nodes as EventStoreDB uses a quorum based algorithm to handle high availability. We recommended you define an odd number of nodes to avoid split brain problems.

Common values for the ‘ClusterSize’ setting are three or five (to have a majority of two nodes and a majority of three nodes).

::: tip Next steps
[Read here](node-roles.md) for more information on the roles available for nodes in an EventStoreDB cluster.
:::

## Manager nodes

Each physical (or virtual) machine in an EventStoreDB cluster typically runs one manager node and one database node. It's also possible to have multiple database nodes per physical machine, running under one manager node.

Manager nodes have a number of responsibilities:

- Starting the database nodes and supervising them to ensure they rest in case of a crash or termination owing to abnormal circumstances. EventStoreDB calls this the _watchdog_ service.
- Communicate with other manager nodes to find cluster state, and relay that information to the database nodes under management.
- Provide a known endpoint for clients to connect to and discover cluster information.
- On Windows, run as a service.


## Example 1 - a three-machine cluster

This example shows the configuration for a three node cluster, running in the typical setup of one manager node and one database node per physical machine, with cluster discovery via DNS. Each machine has one network interface, therefore uses different ports for the internal and external traffic. All nodes, in this case, are running Windows, so the manager nodes run as Windows services.

The important points for writing configuration files are:

-   Node IP Addresses: 192.168.1.11, 192.168.1.12 and 192.168.13
-   TCP ports: (defaults):
    -   Manager Nodes:
        -   Internal HTTP: 30777
        -   External HTTP: 30778
    -   Database Nodes:
        -   Internal TCP: 1112
        -   External TCP: 1113
        -   Internal HTTP: 2112
        -   External HTTP: 2113
-   DNS Entry Name: cluster1.eventstore.local

To configure the cluster correctly, there are a number of steps to follow:

1.  Set up a DNS entry named `cluster1.eventstore.local` with an A record for each node.
2.  Write the database node configuration file for each machine.
3.  Write the manager node configuration file for each machine.
4.  Write the watchdog configuration file for each machine.
5.  Deploy EventStoreDB and the configuration files to each machine.
6.  (**Windows-specific**) Add HTTP URL ACL entries to allow starting HTTP servers on the required HTTP ports.
7.  (**Windows-specific**) Install the manager as a service and start the service.
8.  (**Linux-specific**) Configure the manager as a daemon.

### DNS entry

It depends on which DNS server you use, but the eventual lookup should read:

```bash
$ nslookup cluster1.eventstore.local

Server: 192.168.1.2
Address:  192.168.1.2#53
Name: cluster.eventstore.local
Address:  192.168.1.11
Name: cluster.eventstore.local
Address:  192.168.1.12
Name: cluster.eventstore.local
Address:  192.168.1.13
```

### Database node configuration

All three nodes are similar in configuration.

The important configuration points are the:

- IP Addresses for internal and external interfaces.
- The ports for each endpoint.
- The location of the database file.
- The size of the cluster
- The endpoints from which to seed gossip (in this case the local manager).

We assume that EventStoreDB stores data on the \_D:\_ drive.

You write the configuration files in YAML, and is the following for the first node:

Filename: _database.yaml_

```yaml
Db: d:\es-data
IntIp: 192.168.1.11
ExtIp: 192.168.1.11
IntTcpPort: 1112
IntHttpPort: 2112
ExtTcpPort: 1113
ExtHttpPort: 2113
DiscoverViaDns: false
GossipSeed: ['192.168.1.11:30777']
ClusterSize: 3
```

For each following node, the IP Addresses change, as does the gossip seed, since it is the manager running on the same physical machine as each node.

### Manager configuration

Again, all three nodes are similar in configuration.

The important configuration points are the:

- IP addresses for the internal and external interfaces.
- The ports for the HTTP endpoints.
- The log location.
- The DNS information about other nodes.

Another important configuration item is which database nodes the manager is responsible for starting. You define this in a separate file (the watchdog configuration), the path to which you specify as `WatchdogConfig` in the manager configuration.

You write the configuration files in YAML, and is the following for the first node:

Filename: _manager.yaml_

```yaml
IntIp: 192.168.1.11
ExtIp: 192.168.1.11
IntHttpPort: 30777
ExtHttpPort: 30778
DiscoverViaDns: true
ClusterDns: cluster1.eventstore.local
ClusterGossipPort: 30777
EnableWatchdog: true
WatchdogConfig: c:\EventStore-Config\watchdog.esconfig
Log: d:\manager-log
```

### Watchdog configuration

The watchdog configuration file details which database nodes the manager is responsible for starting and supervising. Unlike the other configuration files, the manager configuration uses a custom format instead of YAML. Each node for which the manager is responsible has one line in the file, which starts with a `#` symbol and then details the command line options given to the database node when it starts it. Under normal circumstances, this is the path to the database node's configuration file.

For the first node in the example cluster, the watchdog configuration file reads as follows:

```
# --config c:\EventStore-Config\database.yaml
```

### Deploying EventStoreDB software and configuration

With configuration files for each node written, you can now deploy EventStoreDB and the configuration. Although it's possible to use relative paths when writing configuration files, it's preferable to use absolute paths to reduce the potential for confusion.

In this example, EventStoreDB is deployed on each node in `C:\EventStore-HA-v`, and the configuration files for that node are deployed into `C:\EventStore-Config`. No installation process is necessary, you unzip the packaged distribution into your preferred location.

### Adding HTTP ACL entries for HTTP servers (Windows-specific)

<!-- TODO: Check this -->

To allow for non-elevated users to run HTTP servers on Windows, you must add entries to the access control list using `netsh`. By default, the manager node runs as `NT AUTHORITY\Local Service`, so this is the user who must have permission to run the HTTP server.

The commands used to add these entries on node one are as follows (Run as an elevated user):

```powershell
# Database Node Internal HTTP Interface
netsh http add urlacl url=http://192.168.1.11:2112/ user="NT AUTHORITY\LOCAL SERVICE"

# Database Node External HTTP Interface
netsh http add urlacl url=http://192.168.1.11:2113/ user="NT AUTHORITY\LOCAL SERVICE"

# Manager Node Internal HTTP Interface
netsh http add urlacl url=http://192.168.1.11:30777/ user="NT AUTHORITY\LOCAL SERVICE"

# Manager Node External HTTP Interface
netsh http add urlacl url=http://192.168.1.11:30778/ user="NT AUTHORITY\LOCAL SERVICE"
```

### Configure the manager node as a service (Windows-specific)

You can install manager nodes as a Windows service so they can start on boot rather than running in interactive mode. Each manager service is given an instance name, which becomes the name of the service (and part of the description for easy identification). The service is installed by default with a startup type of "Automatic (Delayed Start)".

#### Installing the service

To install the manager node on machine 1, use the following command:

```powershell
C:\EventStore-HA-v\> EventStore.WindowsManager.exe install -InstanceName es-cluster1 -ManagerConfig C:\EventStore-Config\manager.yaml
```

The service is then visible in the services list, with a description of "EventStoreDB Manager (es-cluster1)".

#### Uninstalling the service

To uninstall the manager node service, use the following command (where the instance name matches the name used during installation).

```powershell
C:\EventStore-HA-v\> EventStore.WindowsManager.exe uninstall -InstanceName es-cluster1
```

#### Manually starting and stopping the service

- To start the manager node use the `net start es-cluster1` command.
- To stop the manager node use the `net stop es-cluster1` command.
