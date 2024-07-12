# What's New

This page describes new features and other changes in EventStoreDB 24.6.

## New features

- **Legacy TCP Client API plugin** <Badge type="warning" text="Commercial" vertical="middle" />
- **macOS arm64 support for development purposes**
- **Additional metrics**

### Legacy TCP Client API plugin <Badge type="warning" text="Commercial" vertical="middle" />

EventStoreDB v23.10 is the last release to natively support the legacy TCP-based client protocol.
Version 24.2 removed it completely.

This release introduces a new plugin that allows to connect to the database with the legacy TCP API.
This is a stop-gap solution for customers who have not yet upgraded to the gRPC API.

Read more about enabling and configuration of the TCP API plugin in the [documentation](networking.md#external-tcp).

### macOS arm64 support for development

An alpha build was available for running EventStoreDB on arm64 processors since EventStoreDB v22.10 using a [Docker image](https://hub.docker.com/r/eventstore/eventstore/tags?page=&page_size=&ordering=&name=arm64).

Since version 24.6 EventStoreDB can now be built from source and executed on macOS for Apple Silicon.

### Additional metrics

Continuing the work on observability the following [metrics](metrics.md) are now available.

* [Election counter](metrics.md#elections-count): `eventstore_elections_count`
* [Projections](metrics.md#projections).
    * Projection status: `eventstore_projection_status`
    * Percent progress: `eventstore_projection_progress`
    * Events processed since restart: `eventstore_projection_events_processed_after_restart_total`
* [Persistent Subscriptions](metrics.md#persistent-subscriptions):
    * Connection count: `eventstore_persistent_sub_connections`
    * Total in-flight messages : `eventstore_persistent_sub_in_flight_messages`
    * Total number of parked messages: `eventstore_persistent_sub_parked_messages`
    * Oldest Parked Message: `eventstore_persistent_sub_oldest_parked_message_seconds`
    * Total Number of Item processed: `eventstore_persistent_sub_items_processed`
    * Last Seen Message:
        * `eventstore_persistent_sub_last_known_event_number`
        * `eventstore_persistent_sub_last_known_event_commit_position`
    * Last checkpoint event:
        * `eventstore_persistent_sub_checkpointed_event_number`
        * `eventstore_persistent_sub_checkpointed_event_commit_position`

## Feature enhancements

- **Performance improvements**
- **X.509 authentication in client libraries**
- **Metric support for Linux, FreeBSD, macOS**
- **Allow to specify the status Code for health requests**
- **Fix: Events in explicit transactions can be missing in `$all` reads**
- **Miscellaneous**

### Performance improvements

Several performance improvements have been made to EventStoreDB.

#### Reduced FileHandle usage by 80%

The number of file handles used by the database has been reduced by 80%.

This improvement will mostly benefit large databases
* Reduces the likelihood of hitting the default OS file handle limit
* Lower the overall memory footprint

#### Faster Startup For Scavenged databases

Large database with thousands of scavenged chunks now start faster.
A 10x improvement has been observed in testing.

### X.509 authentication in client APIs <Badge type="warning" text="Commercial" vertical="middle" />

Refer to the [clients documentation](@clients/authentication.md) for instructions on how to enable and use X.509 certificates with the EventStoreDB clients.

### Metrics support for Linux, FreeBSD, OSX

The following [System metrics](metrics.md#system) are now available on the following platforms:
* `eventstore_sys_load_avg`: Linux, FreeBSD, OSX, Windows
* `eventstore_sys_cpu`: Linux, FreeBSD, OSX

The following [Process metrics](metrics.md#process) is now available on the following platforms:
* `eventstore_disk_io_bytes`: Linux, Windows and OSX

### Allow to specify the HTTP status Code for health requests

The HTTP status code to be returned by the `/health/live` endpoint can be provided in the query string.
E.g. `/health/live?liveCode=200`.

This is useful for liveness probe expecting specific status code.

### Fix - Events in explicit transactions can be missing from $all reads

This bug only appears with events written in explicit transactions with the deprecated TCP API.
They will not appear on filtered $all reads and subscriptions under specific circumstances.

This [fix](https://github.com/EventStore/EventStore/pull/4251) will also be back ported to v23.10.2 and v22.10.6

### Miscellaneous

* Index merge will now continue without bloom filters if there is not enough memory to store them.
* Use the advertised addresses for identifying nodes in the scavenge log.
* Change status of incomplete scavenges from `Failed` to `Interrupted`.
* Log an error when the node certificate does not have the necessary usages.
* Add more information to the current database options API response.
* Projections will now log a  warning when the projection state size becomes greater than 8 MB.

## Breaking changes

### Persistent subscription checkpoint event type

The persistent subscription checkpoint event type has been changed from `SubscriptionCheckpoint` to `$SubscriptionCheckpoint`.  
Checkpoints written before upgrading will still have the old event type and will continue to work.

### Deposed leader truncation: Two restarts needed

Previously a deposed leader that needed offline truncation would:
* Set a truncate checkpoint and shutdown
* Upon restart, truncate its log and join the cluster

The process is now:
* Set a truncate checkpoint and shutdown
* Upon restart, truncate its log, shutdown

This will affect nodes that run under a Service Manager configured to disallow successive stops of the process, like [systemd](https://systemd.io/) with `StartLimitIntervalSec` and `StartLimitBurst`.

### File-copy backup and restore: Two restarts needed

Part of the file copy [backup and restore procedure](operations.md#simple-full-backup-restore) requires copying the chaser checkpoint file over the truncate checkpoint file.   
This might trigger the database to truncate the log on startup.

Previously the process was:
* Set the truncate checkpoint
* Start the node.

The process is now:
* Set the truncate checkpoint
* Start the node
    * if needed, the log will be truncated and the node will shut down
* Start the node

This will affect nodes that run under a Service Manager configured to disallow successive stops of the process, like [systemd](https://systemd.io/) with `StartLimitIntervalSec` and `StartLimitBurst`

### Unbuffered config setting

The `--unbuffered` config setting is deprecated and has no effect when set to `true`.
This used to enable unbuffered and direct IO when writing to the file system and is no longer supported.

You can read more about any breaking changes and what you should be aware of during an upgrade in the [Upgrade Guide](upgrade-guide.md).
