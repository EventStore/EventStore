---
order: 2
---

# What's New

## New features

* Connectors:
    * Kafka
    * HTTP
    * RabbitMQ
* AutoScavenge Plugin: Schedule and execute scavenges automatically across a cluster.
* Stream Policy Plugin: Define stream access policies based on stream prefixes, rather than using stream ACLs.
* Encryption-at-rest Plugin: Encrypt EventStoreDB chunks to secure them against attackers with file access to the database.
* License keys: A license key is now required to use some features and plugins.

### Connectors

We have improved and expanded on the Connectors preview that was introduced in 24.2.0.

The Connectors plugin is enabled by default.
You can use the HTTP sink without a license, but a license is required for all other connectors.

Refer to the [documentation](TODO) for instructions on setting up and configuring connectors and sinks.

#### Kafka Sink <Badge type="warning" vertical="middle" text="License"/>

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

#### RabbitMQ Sink

The RabbitMQ sink connector sends messages from EventStoreDB to a RabbitMQ exchange using a specified routing key.

It efficiently handles message delivery by abstracting the complexities of RabbitMQ's exchange and queue management, ensuring that messages are routed to the appropriate destinations based on the provided routing key.

This sink is designed for high reliability and supports graceful error handling and recovery mechanisms to ensure consistent message delivery in a production environment.

Refer to the [documentation](TODO) for instructions on setting up a RabbitMQ sink.

### Auto-Scavenge Plugin <Badge type="warning" vertical="middle" text="License"/>

The Autoscavenge plugin automatically schedules _cluster scavenges_ which are composed of multiple _node scavenges_. Only one node scavenge can be executed at a time in the cluster. The Autoscavenge plugin allows to schedule said _cluster scavenges_.

This plugin is bundled in EventStoreDB version of 24.10.0, but is disabled by default.

Refer to the [documentation](../operations/auto-scavenge.md) for instructions on how to enable and use this plugin.

### Stream Policy Plugin <Badge type="warning" vertical="middle" text="License"/>

Define stream access policies in one place based on stream prefixes, rather than using stream ACLs.

Stream access policies can be created to grant users or groups read, write, delete, or metadata access; and then these policies can be applied to streams based on their prefix, or to system or user streams in general.

The Stream Policy plugin requires a license to use, and is disabled by default.

Refer to the [documentation](../configuration/security.md#stream-policy-authorization-plugin) for more information about using and configuring this plugin.

### Encryption-at-rest Plugin <Badge type="warning" vertical="middle" text="License"/>

Encrypt EventStoreDB chunks to secure them against attackers with file access to the database.

This plugin aims to protect against an attacker who obtains access to the physical disk. In contrast to volume or filesystem encryption, file level encryption provides some degree of protection for attacks against the live system or remote exploits as the plaintext data is not directly readable.

The Encryption-at-rest plugin requires a license to use, and is disabled by default.
If Encryption-at-rest is enabled, it is not possible to roll back to an unencrypted database after a new chunk has been created, or if a chunk has been scavenged.

Refer to the [documentation](../configuration/security.md#encryption-at-rest) for more information about using and configuring this plugin.

### License Keys

Some features of EventStoreDB now require a license key to access.

You will need to provide a license key if you want to enable or use the following features:
* Auto-Scavenge
* Kafka Connectors
* Stream Policies
* Encryption-at-rest
* Ldaps Authentication
* OAuth Authentication
* Logs Endpoint
* OTLP Endpoint

Refer to the [documentation](../configuration/license-keys.md) for more information about using a license key.