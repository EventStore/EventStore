# Docker

<!-- **TODO: explain more about using Docker and Compose. Volume mappings and other relevant configuration** -->

## Run with Docker

EventStoreDB has a Docker image available for any platform that supports Docker.

Pull the Docker image:

```bash
docker pull eventstore/eventstore:release-5.0.9
```

Run the container:

```bash
docker run --name eventstore-node -it -p 2113:2113 -p 1113:1113 eventstore/eventstore:release-5.0.9
```

Refer to the [image overview](https://hub.docker.com/r/eventstore/eventstore/) for more information.

The container won't accept command line parameters to the server executable. We recommend configuring EventStoreDB using the configuration file and mapping it as a volume.

In order to sustainably keep the data, we also recommend mapping the database and index volumes.

## Use Docker Compose

EventStoreDB has a Docker image available for any platform that supports Docker. In order to save keystrokes it is possible to run EventStoreDB via Docker Compose.

Create file `docker-compose.yaml` with following content:

@[code{curl}](../samples/docker-compose.yaml)

Run the instance:

```bash
docker-compose up
```

The Compose file above runs EventStoreDB as a single instance.
