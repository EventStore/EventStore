# MongoDB Sink Connector

## Overview

The MongoDB sink connector pulls messages from an EventStoreDB stream and stores the messages to a collection.

## Features

- [At least once delivery](#at-least-once-delivery)
- [Transformation](#transformation)

### At least once delivery

The MongoDB Sink connector guarantees at least once delivery by retrying failed
requests based on configurable resilience settings. It will continue to attempt
delivery until the event is successfully sent or the maximum number of retries
is reached, ensuring each event is delivered at least once.

**Configuration Example**

```json
{
    "Resilience:Enabled": true,
    "Resilience:RequestTimeoutMs": 3000,
    "Resilience:MaxRetries": -1,
    "Resilience:TransientErrorDelay": "00:00:05",
    "Resilience:ReconnectBackoffMaxMs": 20000,
    "Resilience:MessageSendMaxRetries": 2147483647
}
```

### Transformation

The MongoDB Sink connector supports transformation of event data before sending it
to the destination collection. This feature allows you to modify the event data or
metadata, or to add additional information to the record headers.

## Usage

:::: code-group
::: code-group-item Powershell

```powershell
$JSON = @"
{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Mongo.MongoSink",
    "ConnectionString": "mongo://admin:changeit@localhost:27017",
    "Collection": "some-collection",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/mongo-sink-connector
```

:::
::: code-group-item Bash

```bash
JSON='{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Mongo.MongoSink",
    "ConnectionString": "mongo://admin:changeit@localhost:27017",
    "Collection": "some-collection",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -d "$JSON" \
  http://localhost:2113/connectors/mongo-sink-connector
```

:::
::::

## Settings

::: note
The MongoDB sink connector inherits a set of common settings that are used to configure the connector. The settings can
be found in
the [Common Settings](../settings.md) page.
:::

The MongoDB Sink Connector can be configured with the following options:

| Name                                       | Details                                                                                                                                                                                                     |
|--------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Collection`                               | _required_<br><br>**Type**: string<br><br>**Description:** The collection name that resides in the database to push records to.                                                                             |
| `ConnectionString`                         | **Type**: string<br><br>**Description:**  The MongoDB URI to which the connector connects. <br><br>See [connection string URI format](https://www.mongodb.com/docs/manual/reference/connection-string/)     |
| `Batching:BatchSize`                       | **Type**: string<br><br>**Description:** Threshold batch size at which the sink will push the batch of records to the MongoDB collection.                                                                   |
| `Batching:BatchTimeoutMs`                  | **Type**: string<br><br>**Description:**  Threshold time in milliseconds at which the sink will push the current batch of records to the MongoDB collection, regardless of the batch size.                  |
| `DefaultHeaders`                           | **Type**: string<br><br>**Description:** Headers included in all produced messages.<br><br>**Default**: `Accept-Encoding:*`                                                                                 |
| `DocumentIdExtraction:Enabled`             | **Type**: boolean<br><br>**Description:** Enables document Id extraction.<br><br>**Default**: false.<br><br>**Note** By default the document Id is derived from the records key                             |
| `DocumentIdExtraction:Source`              | **Type**: PartitionKeySource<br><br>**Description:** Source for extracting the partition key.<br><br>**Accepted Values:**`Stream`, `StreamSuffix`, `Headers`, `RecordKey`<br><br>**Default**: `Unspecified` |
| `DocumentIdExtraction:Expression`          | **Type**: string<br><br>**Description:** Regular expression for extracting the partition key.                                                                                                               |
| `Resilience:Enabled`                       | **Type**: boolean<br><br>**Description:** Enables resilience features for message handling.<br><br>**Default**: `true`                                                                                      |
| `Resilience:RequestTimeoutMs`              | **Type**: int<br><br>**Description:** Timeout for requests or message handling operations.<br><br>**Default**: `3000`                                                                                       |
| `Resilience:MaxRetries`                    | **Type**: int<br><br>**Description:** Maximum number of retry attempts.<br><br>**Default**: `-1` (unlimited)                                                                                                |
| `Resilience:TransientErrorDelay`           | **Type**: TimeSpan<br><br>**Description:** Delay between retries for transient errors.<br><br>**Default**: `00:00:00`                                                                                       |
| `Resilience:FirstDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the first backoff delay in milliseconds.<br><br>**Default**: `60000`                                                                                  |
| `Resilience:FirstDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the first backoff attempt in milliseconds.<br><br>**Default**: `5000`                                                                                       |
| `Resilience:SecondDelayBound:UpperLimitMs` | **Type**: int<br><br>**Description:** Upper limit for the second backoff delay in milliseconds.<br><br>**Default**: `3600000`                                                                               |
| `Resilience:SecondDelayBound:DelayMs`      | **Type**: int<br><br>**Description:** Delay for the second backoff attempt in milliseconds.<br><br>**Default**: `600000`                                                                                    |
| `Resilience:ThirdDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the third backoff delay in milliseconds.<br><br>**Default**: `3600001`                                                                                |
| `Resilience:ThirdDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the third backoff attempt in milliseconds.<br><br>**Default**: `3600000`                                                                                    |
