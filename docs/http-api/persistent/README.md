# Persistent Subscriptions

This document explains how to use HTTP API for setting up and consuming persistent subscriptions and competing consumer subscription groups. For an overview on competing consumers and how they relate to other subscription types please see our [getting started guide](../introduction/reading-subscribing-events.md).

::: tip
The Administration UI includes a _Competing Consumers_ section where you are able to create, update, delete and view subscriptions and their statuses.
:::

## Creating a Persistent Subscription

Before interacting with a subscription group, you need to create one. You receive an error if you try to create a subscription group more than once. This requires [admin permissions](../security/README.md).

<!-- TODO: File inclusion for the below? -->

| URI                                           | Supported Content Types | Method |
| --------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}` | `application/json`      | PUT    |

### Query Parameters

| Parameter           | Description                                   |
| ------------------- | --------------------------------------------- |
| `stream`            | The stream the persistent subscription is on. |
| `subscription_name` | The name of the subscription group.           |

### Body

| Parameter                     | Description                                                                                        |
| ----------------------------- | -------------------------------------------------------------------------------------------------- |
| `resolveLinktos`              | Tells the subscription to resolve link events.                                                     |
| `startFrom`                   | Start the subscription from the position-of the event in the stream.                               |
| `extraStatistics`             | Tells the backend to measure timings on the clients so statistics will contain histograms of them. |
| `checkPointAfterMilliseconds` | The amount of time the system should try to checkpoint after.                                      |
| `liveBufferSize`              | The size of the live buffer (in memory) before resorting to paging.                                |
| `readBatchSize`               | The size of the read batch when in paging mode.                                                    |
| `bufferSize`                  | The number of messages that should be buffered when in paging mode.                                |
| `maxCheckPointCount`          | The maximum number of messages not checkpointed before forcing a checkpoint.                       |
| `maxRetryCount`               | Sets the number of times a message should be retried before considered a bad message.              |
| `maxSubscriberCount`          | Sets the maximum number of allowed TCP subscribers.                                                |
| `messageTimeoutMilliseconds`  | Sets the timeout for a client before the message will be retried.                                  |
| `minCheckPointCount`          | The minimum number of messages to write a checkpoint for.                                          |
| `namedConsumerStrategy`       | RoundRobin/DispatchToSingle/Pinned                                                                 |

## Updating a Persistent Subscription

You can edit the settings of an existing subscription while it is running. This drops the current subscribers and resets the subscription internally. This requires admin permissions.

| URI                                           | Supported Content Types | Method |
| --------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}` | `application/json`      | POST   |

### Query Parameters

| Parameter           | Description                                      |
| ------------------- | ------------------------------------------------ |
| `stream`            | The stream to the persistent subscription is on. |
| `subscription_name` | The name of the subscription group.              |

### Body

_Same parameters as "Creating a Persistent Subscription"_

## Deleting a Persistent Subscription

| URI                                           | Supported Content Types | Method |
| --------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}` | `application/json`      | DELETE |

### Query Parameters

| Parameter           | Description                                      |
| ------------------- | ------------------------------------------------ |
| `stream`            | The stream to the persistent subscription is on. |
| `subscription_name` | The name of the subscription group.              |

## Reading a stream via a Persistent Subscription

By default, reading a stream via a persistent subscription returns a single event per request and does not embed the event properties as part of the response.

| URI                                                                                                                                                                  | Supported Content Types                                                                      | Method |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name} /subscriptions/{stream}/{subscription_name}?embed={embed} /subscriptions/{stream}/{subscription}/{count}?embed={embed}` | `application/vnd.eventstore.competingatom+xml application/vnd.eventstore.competingatom+json` | GET    |

### Query Parameters

| Parameter           | Description                                                  |
| ------------------- | ------------------------------------------------------------ |
| `stream`            | The stream the persistent subscription is on.                |
| `subscription_name` | The name of the subscription group.                          |
| `count`             | How many events to return for the request.                   |
| `embed`             | `None`, `Content`, `Rich`, `Body`, `PrettyBody`, `TryHarder` |

Read [Reading Streams](../introduction/reading-streams.md) for information on the different embed levels.

### Response

@[code](@httpapi/persistent-subscriptions/read-stream-response.json)

## Acknowledgements

Clients must acknowledge (or not acknowledge) messages in the competing consumer model. If the client fails to respond in the given timeout period, the message is retried. You should use the `rel` links in the feed for acknowledgements not bookmark URIs as they are subject to change in future versions.

For example:

```json
{
  "uri": "http://localhost:2113/subscriptions/newstream/competing_consumers_group1/ack/c322e299-cb73-4b47-97c5-5054f920746f",
  "relation": "ack"
}
```

### Ack multiple messages

| URI                                                                | Supported Content Types | Method |
| ------------------------------------------------------------------ | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/ack?ids={messageids}` | `application/json`      | POST   |

#### Query Parameters

| Parameter           | Description                                    |
| :------------------ | :--------------------------------------------- |
| `stream`            | The stream the persistent subscription is on.  |
| `subscription_name` | The name of the subscription group.            |
| `messageids`        | The ids of the messages that needs to be ACKed |

### Ack a single message

| URI                                                           | Supported Content Types | Method |
| ------------------------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/ack/{messageid}` | `application/json`      | POST   |

#### Query Parameters

| Parameter           | Description                                      |
| ------------------- | ------------------------------------------------ |
| `stream`            | The stream to the persistent subscription is on. |
| `subscription_name` | The name of the subscription group.              |
| `messageid`         | The id of the message that needs to be acked     |

<!-- Has this been explained? -->

### Nack multiple messages

| URI                                                                                 | Supported Content Types | Method |
| ----------------------------------------------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/nack?ids={messageids}?action={action}` | `application/json`      | POST   |

#### Query Parameters

| Parameter           | Description                                                                                                                                                                                                                          |     |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --- |
| `stream`            | The stream to the persistent subscription is on.                                                                                                                                                                                     |     |
| `subscription_name` | The name of the subscription group.                                                                                                                                                                                                  |     |
| `action`            | <ul><li>**Park**: Don't retry the message, park it until a request is sent to reply the parked messages</li><li>**Retry**: Retry the message</li><li>**Skip**: Discard the message</li><li>**Stop**: Stop the subscription</li></ul> |     |
| `messageid`         | The id of the message that needs to be acked                                                                                                                                                                                         |     |

### Nack a single message

| URI                                                                            | Supported Content Types | Method |
| ------------------------------------------------------------------------------ | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/nack/{messageid}?action={action}` | `application/json`      | POST   |

## Replaying parked messages

| URI                                                        | Supported Content Types | Method |
| ---------------------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/replayParked` | `application/json`      | POST   |

## Getting information for all subscriptions

| URI              | Method |
| ---------------- | ------ |
| `/subscriptions` | GET    |

### Response

@[code](@httpapi/persistent-subscriptions/get-all-subscriptions-response.json)

## Get subscriptions for a stream

| URI                       | Supported Content Types | Method |
| ------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}` | `application/json`      | GET    |

### Response

@[code](@httpapi/persistent-subscriptions/get-subscriptions-for-stream-response.json)

## Getting a specific subscription

| URI                                                | Supported Content Types | Method |
| -------------------------------------------------- | ----------------------- | ------ |
| `/subscriptions/{stream}/{subscription_name}/info` | `application/json`      | GET    |

### Response

@[code](@httpapi/persistent-subscriptions/get-subscription-response.json)

<!-- TODO: Is this better as a general subscriptions page? -->
<!-- TODO: Somehow get this better integrated with API docs -->
<!-- TODO: Still to do -->
