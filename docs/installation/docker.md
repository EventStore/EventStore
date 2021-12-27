# Docker

You can run EventStoreDB in Docker container as a single node, using insecure mode. It's good enough in most cases to try out the product and for local development purposes.

It's also possible to run a three-node cluster with or without SSL using Docker Compose. Such a setup is closer to what you'd run in production.

## Run with Docker

EventStoreDB has a Docker image available for any platform that supports Docker.

The following command will start the EventStoreDB node using default HTTP port, without security. You can then connect to it using one of the gRPC clients and `esdb://localhost:2113?tls=false` connection string. The Admin UI will be accessible, but the Stream Browser won't work (it needs AtomPub to be enabled).

```bash
docker run --name esdb-node -it -p 2113:2113 -p 1113:1113 \
    eventstore/eventstore:latest --insecure --run-projections=All
```

If you want to start the node with legacy protocols enabled (TCP and AtomPub), you need to add a couple of other options:

```bash
docker run --name esdb-node -it -p 2113:2113 -p 1113:1113 \
    eventstore/eventstore:latest --insecure --run-projections=All \
    --enable-external-tcp --enable-atom-pub-over-http
```

The command above would run EventStoreDB as a single node without SSL and with the legacy TCP protocol enabled, so you can try out your existing apps with the latest database version.

Then, you'd be able to connect to EventStoreDB with gRPC and TCP clients. Also, the Stream Browser will work in the Admin UI.

In order to sustainably keep the data, we also recommend mapping the database and index volumes.

## Use Docker Compose

You can also run a single-node instance or a three-node secure cluster locally using Docker Compose.

### Insecure single node

You can use Docker Compose to run EventStoreDB in the same setup as the `docker run` command mentioned before.

Create file `docker-compose.yaml` with following content:

@[code{curl}](../samples/docker-compose.yaml)

Run the instance:

```bash
docker-compose up
```

The command above would run EventStoreDB as a single node without SSL and with the legacy TCP protocol enabled. You also get AtomPub protocol enabled, so you can get the stream browser to work in the Admin UI.

### Secure cluster

With Docker Compose, you can also run a three-node cluster with security enabled. That kind of setup is something you'd expect to use in production.

Create file `docker-compose.yaml` with following content:

@[code{curl}](../samples/docker-compose-cluster.yaml)

Quite a few settings are shared between the nodes and we use the `env` file to avoid repeating those settings. So, add the `vars.env` file to the same location:

@[code{curl}](../samples/vars.env)

Containers will use the shared volume using the local `./certs` directory for certificates. However, if you let Docker to create the directory on startup, the container won't be able to get write access to it. Therefore, create the `certs` directory manually. You only need to do it once.

```bash
mkdir certs
```

Now you are ready to start the cluster. 

```bash
docker-compose up
```

Check the log messages, after some time the elections process completes and you'd be able to connect to each node using the Admin UI. Nodes should be accessible on the loopback address (`127.0.0.1` or `localhost`) over HTTP and TCP, using ports specified below:

| Node  | TCP port | HTTP port |
| :---- | :------- | :-------- |
| node1 | 1111     | 2111      |
| node2 | 1112     | 2112      |
| node3 | 1113     | 2113      |

You have tell your client to use secure connection for both TCP and gRPC.

| Protocol | Connection string                                                                                     |
| :------- | :---------------------------------------------------------------------------------------------------- |
| TCP      | `GossipSeeds=localhost:1111,localhost:1112,localhost:1113;ValidateServer=False;UseSslConnection=True` |
| gRPC     | `esdb://localhost:2111,localhost:2112,localhost:2113?tls=true&tlsVerifyCert=false`                    |

As you might've noticed, both connection strings have a setting to disable the certificate validation (`ValidateServer=False` for `TCP` and `tlsVerifyCert=false` for `gRPC`). It would prevent the invalid certificate error since the cluster uses a private, auto-generated CA.

However, **we do not recommend using this setting in production**. Instead, you can either add the CA certificate to the trusted root CA store or instruct your application to use such a certificate. See the [instruction of how to do it.](../security/configuration.md#certificate-installation-on-a-client-environment)


