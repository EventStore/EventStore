# What's New

## New features

* Connectors: TODO
    * Kafka
    * HTTP
* AutoScavenge Plugin: Schedule and execute scavenges automatically across a cluster.
* Stream Policy Plugin: Define stream access policies based on stream prefixes, rather than using stream ACLs.
* Encryption-at-rest Plugin: Encrypt EventStoreDB chunks to secure them against attackers with file access to the database.

### Connectors <Badge type="tip" text="Early preview" vertical="middle" /> <Badge type="warning" text="Commercial" vertical="middle" />

TODO

Refer to the [documentation](/connectors/README.md) for instructions on setting up and configuring connectors and sinks.

### AutoScavenge Plugin <Badge type="warning" text="Commercial" vertical="middle" />

The Autoscavenge plugin automatically schedules _cluster scavenges_ which are composed of multiple _node scavenges_. Only one node scavenge can be executed at a time in the cluster. The Autoscavenge plugin allows to schedule said _cluster scavenges_.

This plugin is bundled in EventStoreDB version of 24.10.0, but is disabled by default.

Refer to the [documentation](operations.md#autoscavenge-plugin) for instructions on how to enable and use this feature.

### Stream Policy Plugin <Badge type="warning" text="Commercial" vertical="middle" />

The Stream Policy plugin allows administrators to define stream access policies for ESDB based on stream prefix. This replaces Stream ACLs, and allows you to configure stream access in a single location for a range of streams.

This plugin is bundled in EventStoreDB version of 24.10.0, but is disabled by default.

Refer to the [documentation](security.md#stream-policy-authorization-plugin) for more information about using and configuring this plugin.

### Encryption-at-rest Plugin <Badge type="warning" text="Commercial" vertical="middle" />

The Encryption-At-Rest plugin allows users to encrypt their EventStoreDB database. Currently, only chunk files are encrypted - the indexes are not. The primary objective is to protect against an attacker who obtains access to the physical disk.

In contrast to volume level encryption, file level encryption provides some degree of protection for attacks against the live system or remote exploits as the plaintext data is not directly readable.

This plugin is bundled in EventStoreDB version of 24.10.0, but is disabled by default.

Refer to the [documentation](security.md#encryption-at-rest) for more information about using and configuring this plugin.
