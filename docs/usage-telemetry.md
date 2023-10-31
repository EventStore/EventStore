# Usage telemetry

Starting with version 23.10 LTS, EventStoreDB has introduced a telemetry feature. This collects anonymous, aggregated usage statistics and sends them periodically to Event Store. Telemetry data helps us refine and improve our product based on real usage patterns.

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
  "tag": "oss-v23.10.0",
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

If telemetry collection is disabled (see below), the disclosure is still shown with a different text:

```
Telemetry
---------
EventStoreDB collects usage data in order to improve your experience. The data is anonymous and collected by EventStore Ltd.
You have opted out of sending telemetry by setting the EVENTSTORE_TELEMETRY_OPTOUT environment variable to true.
For more information visit https://eventstore.com/telemetry
```

## When the telemetry messages are sent

Telemetry collection is designed to exclude short-lived instances, such as EventStoreDB containers initiated for integration tests that are subsequently terminated. Telemetry messages are dispatched one hour after a cluster or instance is activated, followed by subsequent messages every 24 hours. Each telemetry transmission is logged, and the log includes the complete telemetry message, providing full transparency into the data being collected and sent to Event Store Ltd.

## How to opt out

You can completely disable telemetry transmission by setting the `EVENTSTORE_TELEMETRY_OPTOUT` environment variable to `true`. EventStoreDB will still collect the information required to produce telemetry messages, and those messages will be produced to console and diagnostic log, but they will not be sent to Event Store Ltd.
