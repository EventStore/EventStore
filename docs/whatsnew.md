# What's New

## New features

* **Connectors** <Badge type="tip" text="Early preview" vertical="middle" />: This new plugin makes it easy to feed data from EventStoreDB into other systems.
* X.509 User Certificates: Authenticate users with x509 user certificates instead of username and passwords.
* Logs Download: Easily download log files over an HTTP endpoint for diagnosing clusters.
* OpenTelemetry Exporter: Export EventStoreDB metrics via the Open Telemetry Protocol to a specified endpoint.

### Connector <Badge type="tip" text="Early preview" vertical="middle" /> <Badge type="warning" text="Commercial" vertical="middle" />

EventStoreDB 24.2.0 introduces a preview of the Connectors plugin, designed to streamline the integration of EventStoreDB with external services. Connectors allow for the filtering and forwarding of events directly to downstream services and remove the need for manual subscription or checkpoint management.

Each connector runs server-side and maintains its own checkpoints, and can be configured to run on nodes with a specific role (i.e. `Leader`, `Follower`, or `ReadOnlyReplica`).

The preview includes two sinks, with more to follow in upcoming releases:
* Console Sink: For testing and development purposes.
* HTTP Sink: For sending events to an HTTP endpoint of an external system.

Connector plugin is an **early preview**. Its management API is likely to change in future releases.

Refer to the [documentation](/connectors/README.md) for instructions on setting up and configuring connectors and sinks.

### X.509 authentication for clients <Badge type="warning" text="Commercial" vertical="middle" />

Recognized for their robust security, X.509 certificates offer an advanced authentication method. This addition caters to users with rigorous security protocols, allowing for a seamless transition from basic authentication methods to X.509 certificate authentication.

In this initial version of the plugin, X.509 certificate authentication works in addition to the basic authentication method. This allows you to continue using basic authentication while gradually transitioning to X.509 certificate authentication if desired.

This plugin is available in the commercial version of 24.2.0, but is disabled by default.

Refer to the [documentation](security.md#user-x509-certificates) for instructions on how to enable and use this feature.

### OpenTelemetry Exporter <Badge type="warning" text="Commercial" vertical="middle" />

The OpenTelemetry Exporter plugin allows you to export EventStoreDB metrics via the OpenTelemetry Protocol to a specified endpoint.
This direct export capability simplifies integration with Application Performance Monitoring (APM) providers, enhancing visibility into EventStoreDB operations without additional configuration overhead.

This feature empowers you to gain deeper insights into your EventStoreDB deployments, ultimately aiding in performance optimization and enhanced operational efficiency.

Refer to the [documentation](metrics.md#otlp-exporter) for more information about using and configuring this plugin.

### Logs download <Badge type="warning" text="Commercial" vertical="middle" />

The Logs Download plugin provides you with the ability to list and download log files for an EventStoreDB instance over HTTP.

This allows developers and users who don't have direct access to the machines where the log files are stored, to easily list and download log files for diagnostic purposes.

This plugin is part of the commercial version in 24.2.0 and is enabled by default.

Refer to the [documentation](diagnostics.md#downloading-log-files)
for more information on how to use this feature.

## Feature enhancements

* Catchup subscription improvements: Seamless transitions between live and catchup subscriptions. Subscriptions no longer drop with "consumer too slow" reason.
* Better support for containerized environments: Optimized defaults for running in containerized environments.


### Catchup subscription improvements

This update addresses the previous challenge where catchup subscriptions that fell behind would be dropped by the server with the message "Consumer too slow". The client would then need to resubscribe from the last checkpoint to continue receiving events.

As of 24.2.0, catchup subscriptions automatically revert to catch-up mode server-side without user intervention, improving reliability and consistency.

Additionally, subscriptions are now able to re-authorize the user running the subscription in response to user access changes. This means that if you remove a user's access to a stream (for example, through ACLs on the stream), any subscriptions that the user has to that stream will be dropped.

### Better support for containerized environments

EventStoreDB 24.2.0 is now able to detect when it’s running in a containerized environment, and will disable certain auto-configuration options. This helps prevent the node from running out of resources and allows for finer tuning of the EventStoreDB instances.

## Breaking changes

### External TCP API removed

The deprecated external TCP API has been removed in 24.2.0. This means that any external clients using the TCP API will no longer work with EventStoreDB versions 24.2.0 and onwards.

A number of configuration options have been removed as part of this. EventStoreDB will not start by default if any of the following options are present in the database configuration:

```
AdvertiseTcpPortToClientAs
DisableExternalTcpTls
EnableExternalTcp
ExtHostAdvertiseAs
ExtTcpHeartbeatInterval
ExtTcpHeartbeatTimeout
ExtTcpPort
ExtTcpPortAdvertiseAs
NodeHeartbeatInterval
NodeHeartbeatTimeout
NodeTcpPort
NodeTcpPortAdvertiseAs
```

The options for the internal TCP API (`ReplicationTcp*/IntTcp*`) are unchanged from version 23.10.0.

You can read more about any breaking changes and what you should be aware of during an upgrade in the [Upgrade Guide](/upgrade-guide.md).
