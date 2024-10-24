# RabbitMQ Sink Connector

This sink is responsible for sending messages to a RabbitMQ exchange using a specified routing key. It efficiently
handles message delivery by abstracting the complexities of RabbitMQ's exchange and queue management, ensuring that
messages are routed to the appropriate destinations based on the provided routing key. This sink is designed for high
reliability and supports graceful error handling and recovery mechanisms to ensure consistent message delivery in a
production environment.

## Features

- [Broker Acknowledgment](#broker-acknowledgment)
- [Authentication](#authentication)
- [Secured connection](#secured-connection)

### Broker Acknowledgment

By default, the connector waits for broker acknowledgment. Enabling broker acknowledgment ensures that each message sent
to RabbitMQ is confirmed by the broker before the publish operation is considered complete:

```json
{
  "WaitForBrokerAck": true
}
```

Disabling broker acknowledgment can significantly increase throughput by allowing the producer to continue sending
messages without waiting for confirmation from the broker. This is ideal for high-throughput scenarios where performance
is prioritized over delivery guarantees, despite a slight increase in the risk of message loss or duplication.

### Authentication

The RabbitMQ Sink Connector supports basic authentication for connecting to the RabbitMQ broker. To enable authentication, you
must provide the username and password in the configuration settings.

```json
{
  "Authentication:Username": "***********",
  "Authentication:Password": "**************************",
}
```

### Secure connection

To establish a secured connection, you can provide a certificate path and optionally a password. The certificate path
specifies the location of the certificate used for encrypting the connection, ensuring data integrity and
confidentiality during transmission. If the certificate is password-protected, you can provide the password to enable
secure access. These configurations allow for enhanced security when communicating with RabbitMQ, protecting sensitive
information from unauthorized access and ensuring compliance with security standards.

```json
{
  "Authentication:Ssl:Enabled": true,
  "Authentication:Ssl:CertPath": "/path/to/certificate",
  "Authentication:Ssl:CertPassphrase": "*********",
}
```

## Settings

::: note
The RabbitMQ sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [Common Settings](../settings.md) page.
:::

The RabbitMQ Sink Connector can be configured with the following options:

<!-- | Option                        | Description                                                                                                                                                                                                                                                   | Required |
|------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|
| `HostName`                         | **Type**: string<br><br>**Description:** Broker's hostname. Default localhost.                                                                                                                                                                                | No       |
| `Port`                             | **Type**: string<br><br>**Description:** Broker's port. Default 5672.                                                                                                                                                                                         | No       |
| `ConnectionName`                   | **Type**: string<br><br>**Description:** Connection name used for logging and diagnostics. The default value is the connector ID.Connection name used for logging and diagnostics. The default value is the connector ID                                      | No       |
| `VirtualHost`                      | **Type**: string<br><br>**Description:** Represents the VirtualHost (vhost) used in RabbitMQ. Default /                                                                                                                                                       | No       |
| `WaitForBrokerAck`                 | **Type**: boolean<br><br>**Description:** Whether the channel waits for broker acknowledgment before considering the send operation complete.<br><br>**Default**: true                                                                                        | No       |
| `Authentication:Username`          | **Type**: string<br><br>**Description:** Username for authentication.                                                                                                                                                                                         | No       |
| `Authentication:Password`          | **Type**: string<br><br>**Description:** Password for authentication.                                                                                                                                                                                         | No       |
| `Exchange:Name`                    | **Type**: string<br><br>**Description:** Exchange's name.                                                                                                                                                                                                     | Yes      |
| `Exchange:Type`                    | **Type**: string<br><br>**Description:** Exchange's type. Default is fanout.                                                                                                                                                                                  | No       |
| `AutoDelete`                       | **Type**: string<br><br>**Description:** Whether the exchange is automatically deleted when no longer in use. Default false                                                                                                                                   | No       |
| `Durable`                          | **Type**: string<br><br>**Description:** Whether the exchange is durable. Default true.                                                                                                                                                                       | No       |
| `RoutingKey`                       | **Type**: string<br><br>**Description:** Routing key.                                                                                                                                                                                                         | Yes      |
| `Resilience:ConnectionTimeoutMs`   | **Type**: int<br><br>**Description:** Connection TCP establishment timeout in milliseconds. 0 for infinite. Default 6000                                                                                                                                      | No       |
| `Resilience:HandshakeTimeoutMs`    | **Type**: int<br><br>**Description:** The protocol handshake timeout in milliseconds. Default 10000                                                                                                                                                           | No       |
| `Resilience:RequestedHeartbeatMs`  | **Type**: int<br><br>**Description:** The requested heartbeat timeout in milliseconds. 0 means "heartbeats are disabled". Should be no lower than 1 second.                                                                                                   | No       |
