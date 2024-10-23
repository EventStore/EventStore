---
Title: "RabbitMQ sink"
Order: 5
---

# RabbitMQ sink

This sink is responsible for sending messages to a RabbitMQ exchange using a specified routing key.

It efficiently handles message delivery by abstracting the complexities of RabbitMQ's exchange and queue management, ensuring that messages are routed to the appropriate destinations based on the provided routing key.

This sink is designed for high reliability and supports graceful error handling and recovery mechanisms to ensure consistent message delivery in a production environment.

## Features

- [Broker acknowledgment](#broker-acknowledgment)
- [Authentication](#authentication)
- [Secured connection](#secured-connection)
- [Routing key extraction](#partition-key-extraction)

## Usage

::: tabs
@tab Powershell
```powershell
$JSON = @"
{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.RabbitMQ.RabbitMqSink",
    "Exchange:Name": "some-exchange",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/rabbitmq-sink-connector
```
@tab Bash
```bash
JSON='{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.RabbitMQ.RabbitMqSink",
    "Exchange:Name": "some-exchange",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -d "$JSON" \
  http://localhost:2113/connectors/rabbitmq-sink-connector
```
:::

### Broker acknowledgment

By default, the connector waits for broker acknowledgment. Enabling broker acknowledgment ensures that each message sent to RabbitMQ is confirmed by the broker before the publish operation is considered complete:

```json
{
  "WaitForBrokerAck": true
}
```

Disabling broker acknowledgment can significantly increase throughput by allowing the producer to continue sending messages without waiting for confirmation from the broker. This is ideal for high-throughput scenarios where performance is prioritized over delivery guarantees, despite a slight increase in the risk of message loss or duplication.

### Authentication

The RabbitMQ sink supports basic authentication for connecting to the RabbitMQ broker. To enable authentication, you must provide the username and password in the configuration settings.

```json
{
  "Authentication:Username": "***********",
  "Authentication:Password": "**************************",
}
```

### Secure connection

To establish a secured connection, you can provide a certificate path and optionally a password.

The certificate path specifies the location of the certificate used for encrypting the connection, ensuring data integrity and confidentiality during transmission. If the certificate is password-protected, you can provide the password to enable secure access.

These configurations allow for enhanced security when communicating with RabbitMQ, protecting sensitive
information from unauthorized access and ensuring compliance with security standards.

```json
{
  "Authentication:Ssl:Enabled": true,
  "Authentication:Ssl:CertPath": "/path/to/certificate",
  "Authentication:Ssl:CertPassphrase": "*********",
}
```

### Routing key extraction

The RabbitMQ sink can extract routing keys from the record based on various sources to ensure that messages are correctly routed in RabbitMQ.

This feature can be configured using the `RoutingKeyExtraction` option, to determine how routing keys are derived from the records.

**Extracts the routing key from the stream ID using a regular expression**

```json
{
  "RoutingKeyExtraction:Enabled": true,
  "RoutingKeyExtraction:Source": "Stream",
  "RoutingKeyExtraction:Expression": "your-regex-pattern"
}
```

If the `Stream` source is selected, the routitng key is extracted from the stream ID based on the provided regular expression. If the regular expression matches part of the stream ID, that matched value is used as the routing key.

**Extracts the routing key from the suffix of the stream ID**

```json
{
  "RoutingKeyExtraction:Enabled": true,
  "RoutingKeyExtraction:Source": "StreamSuffix"
}
```

When the `StreamSuffix` source is chosen, the routing key is derived from the last segment of the stream ID, split by the '-' character. For example, if the stream ID is `order-2021-05-15`, the routing key would be `2021-05-15`.

**Extracts the routing key from the record headers using a regular expression**

```json
{
  "RoutingKeyExtraction:Enabled": true,
  "RoutingKeyExtraction:Source": "Headers",
  "RoutingKeyExtraction:Expression": "your-regex-pattern",
  "RoutingKeyExtraction:HeaderKey": "your-header-key"
}
```

If the `Headers` source is selected, the routing key is extracted from the headers of the record using the provided regular expression and header key.

The connector searches through the headers, and if a match is found based on the regular expression and header key, that value is used as the routing key. If no match is found, the record's key is
used instead.

**Use the record key as the routing key**

```json
{
  "RoutingKeyExtraction:Enabled": true,
  "RoutingKeyExtraction:Source": "RecordKey"
}
```

When the `RecordKey` source is chosen, the routing key is set to the value of the record key.

## Settings

::: note
The RabbitMQ sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [common settings](../settings.md) page.
:::

<!--
The RabbitMQ sink can be configured with the following options:

| Option                        | Description                                                                                                                                                                                                                                                   | Required |
|------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|
| `HostName`                         | **Type**: string<br><br>**Description:** Broker's hostname.                                                                                                                                                                               | Yes      |
| `Port`                             | **Type**: string<br><br>**Description:** Broker's port.                                                                                                                                                                               | Yes      |
| `WaitForBrokerAck`                 | **Type**: boolean<br><br>**Description:** Whether the channel waits for broker acknowledgment before considering the send operation complete.<br><br>**Default**: true                                                                                       | No       |
| `Exchange`                         | **Type**: string<br><br>**Description:** Exchange's name.                                                                                                                            | Yes      |
| `RoutingKey`                       | **Type**: string<br><br>**Description:** Fixed routing key. Used only if `RoutingKeyExtraction` is disabled.                                                                                                                                                                               | Yes      |
| `RoutingKeyExtraction:Enabled`     | **Type**: boolean<br><br>**Description:** Enables routing key extraction. Must be enabled if `RoutingKey` is empty<br><br>**Default**: false                                                                                                                                                         | No       |
| `RoutingKeyExtraction:Source`      | **Type**: PartitionKeySource<br><br>**Description:** Source for extracting the partition key.g<br><br>**Available Values:**`Stream`, `StreamSuffix`, `Headers`, `RecordKey`<br><br>**Default**: `Unspecified`                                                 | No       |
| `RoutingKeyExtraction:Expression`  | **Type**: string<br><br>**Description:** Regular expression for extracting the partition key.                                                                                                                                                                 | No       |
| `Resilience:Enabled`               | **Type**: boolean<br><br>**Description:** Enables resilience features.<br><br>**Default**: `true`                                                                                                                                                             | No       |
| `Resilience:MaxRetries`            | **Type**: int<br><br>**Description:** Maximum number of retry attempts.<br><br>**Default**: `-1` (unlimited)                                                                                                                                                  | No       |
| `Resilience:TransientErrorDelay`   | **Type**: TimeSpan<br><br>**Description:** Delay between retries for transient errors.<br><br>**Default**: `00:00:05`                                                                                                                                         | No       |
| `Resilience:ReconnectBackoffMaxMs` | **Type**: int<br><br>**Description:** Maximum backoff time for reconnect attempts in milliseconds.<br><br>**Default**: `20000`                                                                                                                                | No       |
| `Resilience:MessageSendMaxRetries` | **Type**: int<br><br>**Description:** Maximum retry attempts for sending messages.<br><br>**Default**: `2147483647`                                                                                                                                           | No       |
| `Authentication:Username`          | **Type**: string<br><br>**Description:** Username for authentication.                                                                                                                                                                                         | No       |
| `Authentication:Password`          | **Type**: string<br><br>**Description:** Password for authentication.                                                                                                                                                                                         | No       | -->
