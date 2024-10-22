---
Title: "HTTP sink"
Order: 1
---

# HTTP sink

## Overview

The HTTP sink allows for integration between EventStoreDB and external
APIs over HTTP or HTTPS. This connector consumes events from an EventStoreDB
stream and converts each event's data into JSON format before sending it in the
request body to a specified Url. Events are sent individually as they are
consumed from the stream, without batching. The event data is transmitted as the
request body, and metadata can be included as HTTP headers.

The connector supports Basic Authentication and Bearer Token Authentication.
Additionally, the connector offers resilience features, such as configurable
retry logic and backoff strategies for handling request failures.

## Features

- [At least once delivery](#at-least-once-delivery)
- [Template parameters](#template-parameters)
- [Transformation](#transformation)
- [Authentication](#authentication)

### At least once delivery

The HTTP sink guarantees at least once delivery by retrying failed
requests based on configurable resilience settings. It will continue to attempt
delivery until the event is successfully sent or the maximum number of retries
is reached, ensuring each event is delivered at least once.

If resilience is enabled (the default), the HTTP sink will retry failed requests based on the configured HTTP
status codes (e.g., 404, 408, 5xx). By default, it retries on any non-2xx status code.

**Configuration example**

```json
{
  "Resilience:Enabled": true,
  "Resilience:RequestTimeoutMs": 3000,
  "Resilience:MaxRetries": -1,
  "Resilience:TransientErrorDelay": "00:00:05",
  "Resilience:RetryOnHttpCodes": "101"
}
```

### Template parameters

The HTTP sink supports the use of template parameters in the URL,
allowing for dynamic construction of the request URL based on event data. This
feature enables you to customize the destination URL for each event, making it
easier to integrate with APIs that require specific URL structures.

#### Available template parameters

The following template parameters are available for use in the URL:

| Parameter          | Description                                                     | Example           |
| ------------------ | --------------------------------------------------------------- | ----------------- |
| `{schema-subject}` | The event's schema subject, converted to lowercase with hyphens | `user-registered` |
| `{event-type}`     | Alias for `{schema-subject}`                                    | `user-registered` |
| `{stream}`         | The EventStoreDB stream ID                                      | `user-123`        |
| `{partition-key}`  | The event's partition key                                       | `123`             |

**Usage**

To use template parameters, include them in the `Url` option of your HTTP sink configuration. The parameters will be
replaced with their corresponding values for each event.

Example:

```
"https://api.example.com/{schema-subject}/{partition-key}"
```

For an event with schema subject "TestEvent" and partition key "123", this would result in the URL:

```
https://api.example.com/TestEvent/123
```

### Transformation

The HTTP sink supports transformation of event data before sending it
to the destination URL. This feature allows you to modify the event data or
metadata, or to add additional information to the request body or headers.

Learn more about transformations in the [Transformation](../quickstart.md#applying-transformations) section.

### Authentication

The HTTP sink supports the following authentication methods:

- **None**: No authentication is used.
- **Basic**: The sink uses basic authentication with a username and password.
- **Bearer**: The sink uses bearer token authentication.

The authentication options are configured using the `Authentication` property in the `HttpSinkOptions`.

## Usage

::: tabs
@tab Powershell
```powershell
$JSON = @"
{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Http.HttpSink",
    "Url": "https://api.example.com",
    "Subscription:Filter:Scope": "Stream",
    "Subscription:Filter:Expression": "some-stream",
    "Subscription:InitialPosition": "Earliest"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/http-sink-connector
```
@tab Bash
```bash
JSON='{
  "settings": {
    "InstanceTypeName": "EventStore.Connectors.Http.HttpSink",
    "Url": "https://api.example.com",
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
  http://localhost:2113/connectors/http-sink-connector
```
:::

## Settings

The HTTP sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [Common settings](../settings.md) page.

The HTTP sink can be configured with the following options:

| Name                                       | Details                                                                                                                                        |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `Url`                                      | _required_ **Type**: string<br><br>**Description:** The URL or endpoint to which the request or message will be sent.<br><br>**Default**: `""` |
| `Method`                                   | **Type**: string<br><br>**Description:** The method or operation to use for the request or message.<br><br>**Default**: `"POST"`               |
| `DefaultHeaders`                           | **Type**: string<br><br>**Description:** Headers included in all messages.<br><br>**Default**: `Accept-Encoding:*`                             |
| `PooledConnectionLifetime`                 | **Type**: TimeSpan<br><br>**Description:** How long a connection can be in the pool to be considered reusable.<br><br>**Default**: `00:05:00`  |
| `Authentication:Method`                    | **Type**: string<br><br>**Description:** The authentication method to use.<br><br>**Default**: `None`                                          |
| `Authentication:Basic:Username`            | **Type**: string<br><br>**Description:** The username for basic authentication.<br><br>**Default**: `""`                                       |
| `Authentication:Basic:Password`            | **Type**: string<br><br>**Description:** The password for basic authentication.<br><br>**Default**: `""`                                       |
| `Authentication:Bearer:Token`              | **Type**: string<br><br>**Description:** The token for bearer authentication.<br><br>**Default**: `""`                                         |
| `Resilience:Enabled`                       | **Type**: boolean<br><br>**Description:** Enables resilience features for message handling.<br><br>**Default**: `true`                         |
| `Resilience:RequestTimeoutMs`              | **Type**: int<br><br>**Description:** Timeout for requests or message handling operations.<br><br>**Default**: `3000`                          |
| `Resilience:MaxRetries`                    | **Type**: int<br><br>**Description:** Maximum number of retry attempts.<br><br>**Default**: `-1` (unlimited)                                   |
| `Resilience:TransientErrorDelay`           | **Type**: TimeSpan<br><br>**Description:** Delay between retries for transient errors.<br><br>**Default**: `00:00:00`                          |
| `Resilience:FirstDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the first backoff delay in milliseconds.<br><br>**Default**: `60000`                     |
| `Resilience:FirstDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the first backoff attempt in milliseconds.<br><br>**Default**: `5000`                          |
| `Resilience:SecondDelayBound:UpperLimitMs` | **Type**: int<br><br>**Description:** Upper limit for the second backoff delay in milliseconds.<br><br>**Default**: `3600000`                  |
| `Resilience:SecondDelayBound:DelayMs`      | **Type**: int<br><br>**Description:** Delay for the second backoff attempt in milliseconds.<br><br>**Default**: `600000`                       |
| `Resilience:ThirdDelayBound:UpperLimitMs`  | **Type**: int<br><br>**Description:** Upper limit for the third backoff delay in milliseconds.<br><br>**Default**: `3600001`                   |
| `Resilience:ThirdDelayBound:DelayMs`       | **Type**: int<br><br>**Description:** Delay for the third backoff attempt in milliseconds.<br><br>**Default**: `3600000`                       |
