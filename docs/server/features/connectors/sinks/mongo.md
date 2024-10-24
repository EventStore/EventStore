---
Title: 'MongoDB sink'
Order: 4
---

# MongoDB sink

<Badge type="info" vertical="middle" text="License Required"/>

## Overview

The MongoDB sink pulls messages from an EventStoreDB stream and stores the messages to a collection.

## Quickstart

::: tabs
@tab Powershell

```powershell
$JSON = @"
{
  "settings": {
    "instanceTypeName": "mongo-db-sink",
    "connectionString": "mongodb://127.0.0.1:27020",
    "database": "exampleDB",
    "collection": "users",
    "subscription:filter:scope": "stream",
    "subscription:filter:expression": "example-stream"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/mongo-sink-connector
```

@tab Bash

```bash
JSON='{
  "settings": {
    "instanceTypeName": "mongo-db-sink",
    "connectionString": "mongodb://127.0.0.1:27020",
    "database": "exampleDB",
    "collection": "users",
    "subscription:filter:scope": "stream",
    "subscription:filter:expression": "example-stream"
  }
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -d "$JSON" \
  http://localhost:2113/connectors/mongo-sink-connector
```

:::

Now, every time an event is appended to the `example-stream`, the MongoDB sink connector will send the event data to the specified collection in the database.

## Settings

Adjust these settings to specify the behavior and interaction of your MongoDB sink connector with EventStoreDB, ensuring it operates according to your requirements and preferences.

::: tip
The MongoDB sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [Sink Options](../settings.md#sink-options) page.
:::

The MongoDB sink can be configured with the following options:

| Name               | Details                                                                                                                                                                                                                                                                                          |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `database`         | _required_<br><br>**Type**: string<br><br>**Description:** The name of the database where the records will be stored.                                                                                                                                                                            |
| `collection`       | _required_<br><br>**Type**: string<br><br>**Description:** The collection name that resides in the database to push records to.                                                                                                                                                                  |
| `connectionString` | _required_<br><br>**Type**: string<br><br>**Description:** The MongoDB URI to which the connector connects. <br><br>See [connection string URI format](https://www.mongodb.com/docs/manual/reference/connection-string/)<br><br>**Default**: `mongodb://mongoadmin:secret@localhost:27017/admin` |
| `documentId:source`     | **Type**: string<br><br>**Description:** The attribute used to generate the document id.<br><br>**Default**: `RecordId`<br><br>**Accepted Values:**<br>- `RecordId`, `Stream`, `Headers`, `StreamSuffix`, `PartitionKey`. |
| `documentId:expression` | **Type**: string<br><br>**Description:** The expression used to format the document id based on the selected source. This allows for custom id generation logic.<br><br>**Default**: `250`                                |
| `certificate:rawData`  | **Type**: string<br><br>**Description:** Base64 encoded x509 certificate.<br><br>**Default**: ""                                         |
| `certificate:password` | **Type**: string<br><br>**Description:** The password used to access the x509 certificate for secure connections.<br><br>**Default**: "" |
| `batching:batchSize`      | **Type**: string<br><br>**Description:** Threshold batch size at which the sink will push the batch of records to the MongoDB collection.<br><br>**Default**: `1000`                                                |
| `batching:batchTimeoutMs` | **Type**: string<br><br>**Description:** Threshold time in milliseconds at which the sink will push the current batch of records to the MongoDB collection, regardless of the batch size.<br><br>**Default**: `250` |

## Examples

### Authentication

This MongoDB sink connector currently only supports [SCRAM](./mongo.md#scram) and [X.509 certificate authentication](./mongo.md#x509-certificate-authentication).

**SCRAM**

To use SCRAM for authentication, include the username and password in the
connection string and set the `authMechanism` parameter in the connection string
to either `SCRAM-SHA-1` or `SCRAM-SHA-256` to select the desired MongoDB
authentication mechanism. For more explanations on the connection string URI
refer to the official MongoDB documentation on [Authentication
Mechanism](https://www.mongodb.com/docs/v4.4/core/authentication-mechanisms/#:~:text=To%20specify%20the%20authentication%20mechanism,mechanism%20from%20the%20command%20line.).

::: note
MongoDB version 4.0 and later uses SCRAM-SHA-256 as the default authentication mechanism if the MongoDB server version supports it.
:::

**X.509 certificate authentication**

To use X.509 certificate authentication, include the base64 encoded x509
certificate and the password in the settings. You can use an online tool like
[base64encode](https://www.base64encode.org/) to encode your certificate.

```
{
  "certificate:rawData": "base64encodedstring",
  "certificate:password": "password"
}
```

### Document ID

The id of the document can be generated automatically based on the source specified and expression if needed. The following options are available:

By default, the MongoDB sink uses the `RecordId` as the document ID. This is the unique identifier generated for every record in EventStoreDB.

Here are some examples that demonstrate how to configure the MongoDB sink to generate document IDs based on different sources.

**1. Set document ID from Stream Name**

You can extract part of the stream name using a regular expression (regex) to define the document ID. In this example, the expression captures the stream name up to `-stream`.

```json
{
  "documentId:source": "Stream",
  "documentId:expression": "^(.*)-stream$"
}
```
The `Stream` source uses the full stream name as the base for the document ID,
and the regex pattern `^(.*)-stream$` is used to extract only the part before
`-stream`. This is helpful when streams follow a naming convention and you only
want a portion of it for the document ID.

**2. Extract the Last Part of the Stream Name with StreamSuffix**

If you only need the last segment of the stream name (after a hyphen or similar delimiter), you can use the `StreamSuffix` source. This doesn't require an expression since it automatically extracts the suffix.

```json
{
  "documentId:source": "StreamSuffix"
}
```
The `StreamSuffix` source is useful when stream names follow a structured
format, and you want to use only the trailing part as the document ID. For
example, if the stream is named `user-123`, the document ID would be `123`.

**3. Set Document ID from Headers**

You can generate the document ID by concatenating values from specific event headers. In this case, two header values (`key1` and `key2`) are combined to form the ID.

```json
{
  "documentId:source": "Headers",
  "documentId:expression": "key1,key2"
}
```
The `Headers` source allows you to pull values from the event's metadata. The
`documentId:expression` field lists the header keys (in this case, `key1` and
`key2`), and their values are concatenated to generate the document ID. This is
useful when headers hold important metadata that should define the document's
unique identifier, such as region, user ID, or other identifiers.

::: details Click here to see an example

```json
{
  "key1": "value1",
  "key2": "value2"
}

// outputs "value1-value2"
```
:::

**4. Set Document ID from Partition Key**

If your event has a partition key, you can use it as the document ID. The `PartitionKey` source directly uses this key without requiring an expression.

```json
{
  "documentId:source": "PartitionKey"
}
```

This uses the record's partition key as a unique document ID.