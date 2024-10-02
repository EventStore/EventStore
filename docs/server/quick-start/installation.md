---
title: "Installation"
order: 3
---

## Quick start

EventStoreDB can run as a single node or as a highly-available cluster. For the cluster deployment, you'd need three server nodes.

The installation procedure consists of the following steps:

- Create a configuration file for each cluster node. If you are using any licensed features, ensure that you configure a [license key](#license-keys).
- Install EventStoreDB on each node using one of the available methods.
- Obtain SSL certificates, either signed by a publicly trusted or private certificate authority.
- Copy the configuration files and SSL certificates to each node.
- Start the EventStoreDB service on each node.
- Check the cluster status using the Admin UI on any node.

### Default access

| User  | Password |
|-------|----------|
| admin | changeit |
| ops   | changeit |

### License Keys

Some features of EventStoreDB require a license key to access.

The license key can be provided to EventStoreDB via environment variable or config file in the [usual plugin config location](./plugins.md#json-files).

Environment variable:

```
EventStore__Plugins__Licensing__LicenseKey={Your key}
```

Configuration file:

```
{
  "EventStore": {
    "Plugins": {
      "Licensing": {
        "LicenseKey": "Your key"
      }
    }
  }
}
```

EventStoreDB will not start if features are enabled that require a license key but the license is not provided or is invalid.

## Linux

EventStoreDB has pre-built packages available on Cloudsmith for RedHat or Debian-based distributions.
The name of the EventStoreDB package is `eventstoredb-ee`.

### Debian packages

#### Install with apt-get

Add the repository to your system according to the [instructions on Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-deb).

Then, install the package:

```bash:no-line-numbers
sudo apt-get install eventstoredb-ee=24.10.0~preview1
```

#### Uninstall with apt-get

You can uninstall the package with:

```bash
sudo apt-get remove eventstoredb-ee
```

If you want to also remove any configuration files and user settings, use:

```bash
sudo apt-get purge eventstoredb-ee
```

### RedHat packages

#### Install with yum

Add the repository to your system according to the [instructions on Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-staging-ee/setup/#formats-rpm).

Then, install the package:

```bash:no-line-numbers
sudo yum install eventstoredb-ee-24.10.0~preview1-1.x86_64
```

#### Uninstall with yum

You can uninstall the package with:

```bash
sudo yum remove eventstoredb-ee
```

### Running the eventstore service

Once installed, the server is registered as a service. Therefore, you can start EventStoreDB with:

```bash:no-line-numbers
sudo systemctl start eventstore
```

When you install the EventStoreDB package, the service doesn't start by default. This allows you to change the configuration located at `etc/eventstore/eventstore.conf` and to prevent creating database and index files in the default location.

::: warning
We recommend that when using Linux you set the 'open file limit' to a high number. The precise value depends on your use case, but at least between `30,000` and `60,000`.
:::

### Download the binaries

You can also [download](https://eventstore.com/downloads/) a binary, extract the archive and run from the folder location.

The following command starts EventStoreDB in dev mode with the database stored at the path `./db` and the logs in `./logs`.
Read more about configuring the EventStoreDB server in the [Configuration section](../configuration/README.md).

```bash
./eventstored --dev --db ./db --log ./logs
```

## Windows

::: warning
EventStoreDB doesn't install as a Windows service. You need to ensure that the server executable
starts automatically.
:::

### NuGet

EventStoreDB has NuGet packages available on Cloudsmith, which replaces the previous Chocolatey packages.

Follow the [instructions on Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-nuget) to set up the NuGet sources.

#### Install with Chocolatey

You can install EventStoreDB through Chocolatey:

```powershell:no-line-numbers
choco install eventstoredb-ee -s eventstore-eventstore-preview --version 24.10.0-preview1
```

EventStoreDB can then be run with `EventStore.ClusterNode.exe`:

```powershell:no-line-numbers
EventStore.ClusterNode.exe --config {your config file}
```

#### Uninstall with Chocolatey

You can uninstall EventStoreDB through Chocolatey with:

```powershell:no-line-numbers
choco uninstall eventstoredb-ee
```

### Download the binaries

You can also [download](https://eventstore.com/downloads/) a binary, unzip the archive and run from the folder location.

The following command starts EventStoreDB in dev mode with the database stored at the path `./db` and the logs in `./logs`.
Read more about configuring the EventStoreDB server in the [Configuration section](../configuration/README.md).

```powershell:no-line-numbers
./EventStore.ClusterNode.exe --dev --db ./db --log ./logs
```

## Docker

You can run EventStoreDB in a Docker container as a single node, using insecure mode. It is useful in most
cases to try out the product and for local development purposes.

It's also possible to run a three-node cluster with or without SSL using Docker Compose. Such a setup is
closer to what you'd run in production.

### Run with Docker

EventStoreDB Docker images are now hosted [on Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-preview/setup/#formats-docker) under the registry `docker.eventstore.com/eventstore-preview`.

Pull the container with:

```bash:no-line-numbers
docker pull docker.eventstore.com/eventstore-preview/eventstoredb-ee:latest
```

The following command will start the EventStoreDB node using the default HTTP port, without security. You can then connect to it using one of the clients and the `esdb://localhost:2113?tls=false` connection string. You can also access the Admin UI by opening http://localhost:2113 in your browser.

```bash:no-line-numbers
docker run --name esdb-node -it -p 2113:2113 \
    docker.eventstore.com/eventstore-preview/eventstoredb-ee --insecure --run-projections=All
    --enable-atom-pub-over-http
```

Then, you'd be able to connect to EventStoreDB with gRPC clients. Also, the Stream Browser will work
in the Admin UI.

In order to sustainably keep the data, we also recommend mapping the database and index volumes.

### Use Docker Compose

You can also run a single-node instance or a three-node secure cluster locally using Docker Compose.

#### Insecure single node

You can use Docker Compose to run EventStoreDB in the same setup as the `docker run` command mentioned before.

Create a file `docker-compose.yaml` with the following content:

@[code{curl}](@samples/docker-compose.yaml)

Run the instance:

```bash:no-line-numbers
docker compose up
```

The command above would run EventStoreDB as a single node without SSL. You also get AtomPub protocol enabled, so you can get the stream browser to work in the Admin UI.

::: warning
The legacy TCP client protocol is disabled by default and is no longer be available from version 24.10. 
:::

#### Secure cluster

With Docker Compose, you can also run a three-node cluster with security enabled. This kind of setup is
something you'd expect to use in production.

Create file `docker-compose.yaml` with following content:

@[code{curl}](@samples/docker-compose-cluster.yaml)

Quite a few settings are shared between the nodes and we use the `env` file to avoid repeating those settings.
So, add the `vars.env` file to the same location:

@[code{curl}](@samples/vars.env)

Containers will use the shared volume using the local `./certs` directory for certificates. However, if you
let Docker create the directory on startup, the container won't be able to get write access to it.
Therefore, you should create the `certs` directory manually. You only need to do it once.

```bash:no-line-numbers
mkdir certs
```

Now you are ready to start the cluster.

```bash:no-line-numbers
docker compose up
```

Watching the log messages, you will see that after some time, the elections process completes. Then you're able to connect to each
node using the Admin UI. Nodes should be accessible on the loopback address (`127.0.0.1` or `localhost`) over
HTTP, using ports specified below:

| Node  | HTTP port |
|:------|:----------|
| node1 | 2111      |
| node2 | 2112      |
| node3 | 2113      |

You have to tell your client to use secure connection.

| Protocol | Connection string                                                                  |
|:---------|:-----------------------------------------------------------------------------------|
| gRPC     | `esdb://localhost:2111,localhost:2112,localhost:2113?tls=true&tlsVerifyCert=false` |

As you might've noticed, the connection string has a setting to disable the certificate validation (`tlsVerifyCert=false`). It would prevent the invalid certificate error since the cluster uses a private, auto-generated CA.

However, **we do not recommend using this setting in production**. Instead, you can either add the CA certificate to the trusted root CA store or instruct your application to use such a certificate. See the [security section](../configuration/security.md#certificate-installation-on-a-client-environment) for detailed instructions.

## Building from source

You can also build [EventStoreDB from source](https://github.com/EventStore/EventStore?tab=readme-ov-file#building-eventstoredb). Before doing that, you need to install the .NET 8 SDK. EventStoreDB packages have the .NET Runtime embedded, so you don't need to install anything except the EventStoreDB package.

## Compatibility notes

Depending on how your EventStoreDB instance is configured, some features might not work. Below are some features that are unavailable due to the specified options.

| Feature                       | Options impact                                                                                                                                                                          |
|:------------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Connection without SSL or TLS | EventStoreDB is secure by default. Your clients need to establish a secure connection, unless you use the `Insecure` option.                                                      |
| Authentication and ACLs       | When using the `Insecure` option for the server, all security is disabled. The `Users` menu item is also disabled in the Admin UI.                                                      |
| Projections                   | Running projections is disabled by default and the `Projections` menu item is disabled in the Admin UI. You need to enable projections explicitly by using the `RunProjections` option. |
| AtomPub protocol              | The AtomPub protocol is disabled by default. If you use this protocol, you have to explicitly enable it by using the `EnableAtomPubOverHttp` option.                          |
| Stream browser                | The stream browser feature in Admin UI depends on the AtomPub protocol and is greyed out by default. You need to enable AtomPub (previous line) to make the stream browser work.        |
