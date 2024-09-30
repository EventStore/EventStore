---
order: 3
---

# Built-in sinks

Sinks are Connector targets. They are the final destination for events from EventStoreDB point of view. Each sink requires its own configuration where the configuration options are specific to the sink type.

::: note
Sink configuration will likely change in future versions.
:::

## Console Sink

The console sink just writes to the console of whichever host the connector is running in.

Here is how to create a console sink connector in JSON:

```json
{
  "Sink": "console://"
}
```

## HTTP Sink

The HTTP Sink posts events to specified endpoints and delivers event metadata as HTTP headers. The event payload is sent as the body of the HTTP request. The sink only supports JSON events, and will set the `Content-Type` header to `application/json`.

Here is how to create an HTTP sink connector in JSON:

```json
{
  "Sink": "https://example.com/events"
}
```

The target endpoint URL supports placeholders for the following event metadata:
* `eventType`
* `stream`
* `category`

For example, if you have a service with endpoints per event type, you can use the `eventType` placeholder to route events to the correct endpoint.

```json
{
  "Sink": "https://example.com/events/{eventType}"
}
``` 

The `category` parameter will use all the characters before `-` in the event type. For example, if the event type is `orderCreated-123`, the `category` will be `orderCreated`.

Transient errors will be retried with an exponential backoff strategy. The sink will not retry events that have been successfully delivered. The retry policy is not configurable, and the sink will try to redeliver the event endlessly. It is a current limitation that the sink does not support dead-lettering.