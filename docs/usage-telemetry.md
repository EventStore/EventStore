# Usage telemetry

Starting from version 23.10 LTS, EventStoreDB includes a telemetry feature, which collects anonymous aggregated usage data, and sends it to Event Store from time to time. Telemetry data helps us to understand how our product is used by our users, so we can make the product better.

## What is usage telemetry

EventStoreDB telemetry only tracks non-Personally-Identifiable Information. Collected data does not allow Event Store to fingerprint the users by any of the collected data points.

Examples of the telemetry data collected:

* EventStoreDB version number
* Cluster or instance size
* Node machine size
* Is cluster or instance configured in secure mode
* Legacy TCP client protocol enabled
* Legacy AtomPub protocol enabled
* Number of running projections
* Number of running persistent subscriptions

What EventStoreDB **does not** track:

* Stream names
* Stream metadata
* Event payloads
* Event metadata
* Usernames and passwords

Here's an example of a full telemetry message from a single-node instance:

```json
{
  "version": "23.10.0.0",
  "tag": "",
  "uptime": "00:01:11.0115732",
  "cluster": {
    "leaderId": "81e9ca2a-8acc-41ed-b9d0-65a652b36801",
    "nodeId": "81e9ca2a-8acc-41ed-b9d0-65a652b36801",
    "nodeState": "Leader"
  },
  "configuration": {
    "clusterSize": 1,
    "enableAtomPubOverHttp": true,
    "enableExternalTcp": true,
    "insecure": false,
    "runProjections": "None"
  },
  "database": {
    "epochNumber": 29,
    "firstEpochId": "b02f1ba6-98a3-473f-ade7-dcd1476b2149",
    "activeChunkNumber": 2
  },
  "environment": {
    "coreCount": 24,
    "isContainer": false,
    "isKubernetes": false,
    "processorArchitecture": "X64",
    "totalDiskSpace": 1000186310656,
    "totalMemory": 68631052288
  },
  "gossip": {
    "81e9ca2a-8acc-41ed-b9d0-65a652b36801": "Leader"
  },
  "persistentSubscriptions": {
    "count": 0
  },
  "projections": {
    "customProjectionCount": 0,
    "standardProjectionCount": 5,
    "customProjectionRunningCount": 0,
    "standardProjectionRunningCount": 5,
    "totalCount": 5,
    "totalRunningCount": 5
  }
}
```

## Disclosure

On startup, EventStoreDB produces a message to the console and to the diagnostic logs, which looks similar to:

```
Telemetry
---------
EventStoreDB collects usage data in order to improve your experience. The data is anonymous and collected by Event Store Ltd.
You can opt out of sending telemetry by setting the EVENTSTORE_TELEMETRY_OPTOUT environment variable to true.
For more information visit https://eventstore.com/telemetry
```

The message is not shown if telemetry collection is disabled (see below).

## When the telemetry messages are sent

Telemetry collection is designed to exclude short-lived instances, such as EventStoreDB containers initiated for integration tests that are subsequently terminated. Telemetry messages are dispatched one hour after a cluster or instance is activated, followed by subsequent messages every 24 hours. Each telemetry transmission is logged, and the log includes the complete telemetry message, providing full transparency into the data being collected and sent to Event Store Ltd.

## How to opt out

You can completely disable telemetry collection and transmission by setting the `EVENTSTORE_TELEMETRY_OPTOUT` environment variable to `true`.

