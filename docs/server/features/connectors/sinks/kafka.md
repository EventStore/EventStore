---
Title: "Kafka sink connector"
Order: 3
---

# Kafka sink connector

<Badge type="info" vertical="middle" text="License Required"/>

## Overview

The Kafka Sink Connector writes events to a Kafka topic. It can extract the
partition key from the record based on specific sources such as the stream ID,
headers, or record key and also supports basic authentication and resilience
features to handle transient errors.

## Features

- [At least once delivery](#at-least-once-delivery)
- [Transformation](#transformation)
- [Broker acknowledgment](#broker-acknowledgment)
- [Authentication](#authentication)
- [Partition key extraction](#partition-key-extraction)

### At least once delivery

The Kafka Sink connector guarantees at least once delivery by retrying failed
requests based on configurable resilience settings. It will continue to attempt
delivery until the event is successfully sent or the maximum number of retries
is reached, ensuring each event is delivered at least once.

The Kafka Sink Connector currently retries transient errors based on the following error codes:

- **Local_AllBrokersDown**: All broker connections are down
- **OutOfOrderSequenceNumber**: Broker received an out of order sequence number
- **TransactionCoordinatorFenced**: Indicates that the transaction coordinator sending a WriteTxnMarker is no longer the
  current coordinator for a given producer
- **UnknownProducerId**: Unknown Producer Id.

For detailed information on the listed error codes, refer to
the [Kafka documentation](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.ErrorCode.html).

**Configuration example**

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

The Kafka Sink connector supports transformation of event data before sending it
to the destination Kafka topic. This feature allows you to modify the event data or
metadata, or to add additional information to the record headers.

Learn more about transformations in the [Transformation](../quickstart.md#applying-transformations) section.

### Broker acknowledgment

By default, the connector waits for broker acknowledgment. Enabling broker acknowledgment ensures that each message sent
to Kafka is confirmed by the broker before the send operation is considered complete:

```json
{
  "WaitForBrokerAck": true
}
```

Disabling broker acknowledgment can significantly increase throughput by
allowing the producer to continue sending messages without waiting for
confirmation from the broker. This is ideal for high-throughput scenarios where
performance is prioritized over delivery guarantees.

### Authentication

The Kafka Sink Connector supports basic authentication for connecting to the Kafka broker. To enable authentication, you
must provide the username and password in the configuration settings.

```json
{
  "Authentication:Username": "***********",
  "Authentication:Password": "**************************",
  "Authentication:SecurityProtocol": "SaslSsl"
}
```

To learn more about authentication in Kafka,
see [Authentication using SASL](https://kafka.apache.org/documentation/#security_sasl)

### Partition key extraction

The sink connector can extract partition keys from the record based on various sources to ensure that messages are
correctly partitioned in Kafka. This feature can be configured using the `PartitionKeyExtraction` option, to determine
how partition keys are derived from the records.

**Extracts the partition key from the stream ID using a regular expression**

```json
{
  "PartitionKeyExtraction:Enabled": true,
  "PartitionKeyExtraction:Source": "Stream",
  "PartitionKeyExtraction:Expression": "your-regex-pattern"
}
```

If the `Stream` source is selected, the partition key is extracted from the stream ID based on the provided regular
expression. If the regular expression matches part of the stream ID, that matched value is used as the partition key.

**Extracts the partition key from the suffix of the stream ID**

```json
{
  "PartitionKeyExtraction:Enabled": true,
  "PartitionKeyExtraction:Source": "StreamSuffix"
}
```

When the `StreamSuffix` source is chosen, the partition key is derived from the last segment of the stream ID, split
by the '-' character. For example, if the stream ID is `order-2021-05-15`, the partition key would be `2021-05-15`.

**Extracts the partition key from the record headers using a regular expression**

```json
{
  "PartitionKeyExtraction:Enabled": true,
  "PartitionKeyExtraction:Source": "Headers",
  "PartitionKeyExtraction:Expression": "your-regex-pattern",
  "PartitionKeyExtraction:HeaderKey": "your-header-key"
}
```

If the `Headers` source is selected, the partition key is extracted from the headers of the record using the provided
regular expression and header key. The connector searches through the headers, and if a match is found based on the
regular expression and header key, that value is used as the partition key. If no match is found, the record's key is
used instead.

**Use the record key as the partition key**

```json
{
  "PartitionKeyExtraction:Enabled": true,
  "PartitionKeyExtraction:Source": "RecordKey"
}
```

When the `RecordKey` source is chosen, the partition key is set to the value of the record key.

## Usage

::: tabs
@tab Powershell
```powershell
$JSON = @"
{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Kafka.KafkaSink",
    "Topic": "your-topic",
    "BootstrapServers": "localhost:9092",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/kafka-sink-connector
```
@tab Bash
```bash
JSON='{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Kafka.KafkaSink",
    "Topic": "your-topic",
    "BootstrapServers": "localhost:9092",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -d "$JSON" \
  http://localhost:2113/connectors/kafka-sink-connector
```
:::

## Settings

::: note
The Kafka sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [Common settings](../settings.md) page.
:::

The Kafka Sink Connector can be configured with the following options:

| Name                                       | Details                                                                                                                                                                                                                                            |
| ------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Topic`                                    | _required_<br><br>**Type**: string<br><br>**Description:** The Kafka topic to produce records to.                                                                                                                                                  |
| `BootstrapServers`                         | _required_<br><br>**Type**: string<br><br>**Description:** Comma-separated list of Kafka broker addresses.                                                                                                                                         |
| `WaitForBrokerAck`                         | **Type**: boolean<br><br>**Description:** Whether the producer waits for broker acknowledgment before considering the send operation complete.<br><br>**Default**: true                                                                            |
| `DefaultHeaders`                           | **Type**: string<br><br>**Description:** Headers included in all produced messages.<br><br>**Default**: `Accept-Encoding:*`                                                                                                                        |
| `PartitionKeyExtraction:Enabled`           | **Type**: boolean<br><br>**Description:** Enables partition key extraction.<br><br>**Default**: false                                                                                                                                              |
| `PartitionKeyExtraction:Source`            | **Type**: PartitionKeySource<br><br>**Description:** Source for extracting the partition key.<br><br>**Accepted Values:**`Stream`, `StreamSuffix`, `Headers`, `RecordKey`<br><br>**Default**: `Unspecified`                                        |
| `PartitionKeyExtraction:Expression`        | **Type**: string<br><br>**Description:** Regular expression for extracting the partition key.                                                                                                                                                      |
| `BrokerAddressFamily`                      | **Type**: [BrokerAddressFamily](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.BrokerAddressFamily.html)<br><br>**Description:** Allowed broker IP address families.<br><br>**Default**: `V4` |
| `Compression:Type`                         | **Type**: [CompressionType](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.CompressionType.html)<br><br>**Description:** Kafka compression type.<br><br>**Default**: `Zstd`                   |
| `Compression:Level`                        | **Type**: int<br><br>**Description:** Kafka compression level.<br><br>**Default**: 6                                                                                                                                                               |
| `Resilience:Enabled`                       | **Type**: boolean<br><br>**Description:** Enables resilience features for message handling.<br><br>**Default**: `true`                                                                                                                             |
| `Resilience:RequestTimeoutMs`              | **Type**: int<br><br>**Description:** Timeout for requests or message handling operations.<br><br>**Default**: `3000`                                                                                                                              |
| `Resilience:MaxRetries`                    | **Type**: int<br><br>**Description:** Maximum number of retry attempts.<br><br>**Default**: `-1` (unlimited)                                                                                                                                       |
| `Resilience:TransientErrorDelay`           | **Type**: TimeSpan<br><br>**Description:** Delay between retries for transient errors.<br><br>**Default**: `00:00:00`                                                                                                                              |
| `Resilience:ReconnectBackoffMaxMs`         | **Type**: int<br><br>**Description:** Maximum backoff time in milliseconds for reconnection attempts.<br><br>**Default**: `20000`                                                                                                                  |
| `Resilience:MessageSendMaxRetries`         | **Type**: int<br><br>**Description:** Number of times to retry sending a failing message. **Note:** Retrying may cause reordering unless `enable.idempotence` is set to true.<br><br>**Default**: `2147483647`                                     |
| `Resilience:FirstDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the first backoff delay in milliseconds.<br><br>**Default**: `60000`                                                                                                                         |
| `Resilience:FirstDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the first backoff attempt in milliseconds.<br><br>**Default**: `5000`                                                                                                                              |
| `Resilience:SecondDelayBound:UpperLimitMs` | **Type**: int<br><br>**Description:** Upper limit for the second backoff delay in milliseconds.<br><br>**Default**: `3600000`                                                                                                                      |
| `Resilience:SecondDelayBound:DelayMs`      | **Type**: int<br><br>**Description:** Delay for the second backoff attempt in milliseconds.<br><br>**Default**: `600000`                                                                                                                           |
| `Resilience:ThirdDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the third backoff delay in milliseconds.<br><br>**Default**: `3600001`                                                                                                                       |
| `Resilience:ThirdDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the third backoff attempt in milliseconds.<br><br>**Default**: `3600000`                                                                                                                           |

For Kafka client enum types, please refer to the
official [Kafka .NET client documentation](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.html).
