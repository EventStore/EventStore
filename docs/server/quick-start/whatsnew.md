---
order: 2
---

# What's New

## New features

* Connectors:
    * Kafka
    * HTTP
* AutoScavenge: Schedule and execute scavenges automatically across a cluster.
* Stream Policy: Define stream access policies based on stream prefixes, rather than using stream ACLs.
* Encryption-at-rest: Encrypt EventStoreDB chunks to secure them against attackers with file access to the database.

### Connectors

We have improved and expanded on the Connectors preview that was introduced in 24.2.0.

The Connectors feature is enabled by default.
You can use the HTTP sink without a license, but a license is required for all other connectors.

Refer to the [documentation](TODO) for instructions on setting up and configuring connectors and sinks.

#### Kafka Sink

<Badge type="info" vertical="middle" text="License Required"/>

The Kafka Sink Connector writes events from EventStoreDB to a Kafka topic.

It can extract the partition key from the record based on specific sources such as the stream ID, headers, or record key and also supports basic authentication and resilience
features to handle transient errors.

Refer to the [documentation](TODO) for instructions on setting up a Kafka sink.

#### HTTP Sink

The HTTP Sink connector allows for integration between EventStoreDB and external
APIs over HTTP or HTTPS.

This connector consumes events from an EventStoreDB
stream and converts each event's data into JSON format before sending it in the
request body to a specified Url. Events are sent individually as they are
consumed from the stream, without batching. The event data is transmitted as the
request body, and metadata can be included as HTTP headers.

The connector supports Basic Authentication and Bearer Token Authentication.
Additionally, the connector offers resilience features, such as configurable
retry logic and backoff strategies for handling request failures.

Refer to the [documentation](TODO) for instructions on setting up an HTTP sink.

### Auto-Scavenge

<Badge type="info" vertical="middle" text="License Required"/>

The Autoscavenge feature automatically schedules _cluster scavenges_ which are composed of multiple _node scavenges_. Only one node scavenge can be executed at a time in the cluster. The Autoscavenge feature allows to schedule said _cluster scavenges_.

This feature is disabled by default.

Refer to the [documentation](../operations/auto-scavenge.md) for instructions on how to enable and use this feature.

### Stream Policy

<Badge type="info" vertical="middle" text="License Required"/>

Define stream access policies in one place based on stream prefixes, rather than using stream ACLs.

Stream access policies can be created to grant users or groups read, write, delete, or metadata access; and then these policies can be applied to streams based on their prefix, or to system or user streams in general.

The Stream Policy feature requires a license to use, and is disabled by default.

Refer to the [documentation](../configuration/security.md#stream-policy-authorization) for more information about using and configuring this feature.

### Encryption-at-rest

<Badge type="info" vertical="middle" text="License Required"/>

Encrypt EventStoreDB chunks to secure them against attackers with file access to the database.

This feature aims to protect against an attacker who obtains access to the physical disk. In contrast to volume or filesystem encryption, file level encryption provides some degree of protection for attacks against the live system or remote exploits as the plaintext data is not directly readable.

The Encryption-at-rest feature requires a license to use, and is disabled by default.
If Encryption-at-rest is enabled, it is not possible to roll back to an unencrypted database after a new chunk has been created, or if a chunk has been scavenged.

Refer to the [documentation](../configuration/security.md#encryption-at-rest) for more information about using and configuring this feature.

## Event Store License v2

Starting with the 24.10 LTS release, we are transitioning to a new licensing model: the Event Store License v2 (ESLv2).

A summary of the effects on installing and using EventStoreDB are listed below. Check out [the announcement](https://www.eventstore.com/blog/introducing-event-store-license-v2-eslv2) for the full details.

### Unified binary release

There is no longer a distinction between OSS and commercial binaries for EventStoreDB.

Instead there is a single binary artifact which anyone can access and use, but a license key is required to use enterprise features.

### Enterprise features now require a license key

Customers can unlock the enterprise features of EventStoreDB with a license key. This applies to the previous commercial plugins, and some of the new features in this release.

You will need to provide a license key if you want to enable or use the following features:
* Auto-Scavenge
* Kafka Connectors
* Stream Policies
* Encryption-at-rest
* Ldaps Authentication
* OAuth Authentication
* Logs Endpoint
* OTLP Endpoint

Refer to the [documentation](../quick-start/installation.md#license-keys) for more information about using a license key.

### EventStoreDB binaries have moved to Cloudsmith

All EventStoreDB binaries are now available on [Cloudsmith](https://cloudsmith.io/~eventstore/repos/eventstore-preview/). In addition, the RedHat binaries which were previously commercial-only can now be accessed by all users of EventStoreDB.
