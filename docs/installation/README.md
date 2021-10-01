---
title: "Quick start"
---

## Quick start

EventStoreDB can run as a single node or as a highly-available cluster. For the cluster deployment, you'd need three server nodes.

The installation procedure consists of the following steps:
- Create a configuration file for each cluster node
- Obtain SSL certificates, either signed by a trusted authority, or self-signed
- Install EventStoreDB on each node using one of the available methods
- Copy the configuration files and SSL certificates to each node
- Start the EventStoreDB service on each node
- Check the cluster status using the Admin UI on any node

## Default access

User | Password
---- | --------
admin | changeit
ops | changeit

## Configuration Wizard

[EventStore Configurator](https://configurator.eventstore.com) online tool can help you to go through all the required steps.

You can provide the details about your desired deployment topology, and get the following:
- Generated configuration files
- Instructions for obtaining or generating SSL certificates
- Installation guidelines
- gRPC and TCP client connection details

::: tip Event Store Cloud
Avoid deploying, configuring, and maintaining the EventStoreDB instance yourself by using
[Event Store Cloud](https://www.eventstore.com/event-store-cloud).
:::
