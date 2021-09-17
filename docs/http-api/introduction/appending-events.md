# Appending Events

You append to a stream over HTTP using a `POST` request to the resource of the stream. If the stream does not exist then the stream is implicitly created.

## EventStoreDB media types

EventStoreDB supports a custom media type for posting events, `application/vnd.eventstore.events+json` or `application/vnd.eventstore.events+xml`. This format allows for extra functionality that posting events as `application/json` or `application/xml` does not. For example it allows you to post multiple events in a single batch.

<!-- TODO: And more? Why not use it? And why are these examples not using it? -->

The format represents data with the following jschema (`eventId` must be a UUID).

```json
[
    {
      "eventId"    : "string",
      "eventType"  : "string",
      "data"       : "object",
      "metadata"   : "object"
    }
]
```

## Appending a single event to a new stream

If you issue a `POST` request with data to a stream and the correct content type set it appends the event to the stream, and generates a `201` response from the server, giving you the location of the event. Using the following event, which [you can also download as a file](@httpapi/event.json):

@[code](@httpapi/event.json)

`POST` the following request to create a stream and add an event to it:

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-to-new-stream.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-to-new-stream.sh)
:::
::::

Some clients may not be able to generate a unique identifier (or may not want to) for the event ID. You need this ID for idempotence purposes and EventStoreDB can generate it for you.

If you leave off the `ES-EventId` header you see different behavior:

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-no-id.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-no-id.sh)
:::
::::

In this case EventStoreDB has responded with a `307 Temporary Redirect`. The location points to another URI that you can post the event to. This new URI is idempotent for posting, even without an event ID.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-follow.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-follow.sh)
:::
::::

It's generally recommended to include an event ID if possible as it results in fewer round trips between the client and the server.

When posting to either the stream or to the returned redirect, clients must include the `EventType` header. If you forget to include the header you receive an error.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-no-type.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-no-type.sh)
:::
::::

## Batch append operation

You can append more than one event in a single post by placing multiple events inside of the array representing the events, including metadata.

For example, the below has two events:

@[code](@httpapi/multiple-events.json)

When you append multiple events in a single post, EventStoreDB treats them as one transaction, it appends all events together or fails.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-multiple-events.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-multiple-events.sh)
:::
::::

### Appending events

To append events, issue a `POST` request to the same resource with a new `eventId`:

@[code](@httpapi/event-append.json)

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event.sh)
:::
::: code-group-item Response
@[code{curl}](@httpapi/append-event.sh)
:::
::::

## Data-only events

Version 3.7.0 of EventStoreDB added support for the `application/octet-stream` content type to support data-only binary events. When creating these events, you need to provide the `ES-EventType` and `ES-EventId` headers and cannot have metadata associated with the event. In the example below `SGVsbG8gV29ybGQ=` is the data you `POST` to the stream:

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-data-event.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-data-event.sh)
:::
::::

## Expected version header

The expected version header represents the version of the stream you expect.

For example if you append to a stream at version 1, then you expect it to be at version 1 next time you append. This can allow for optimistic locking when multiple applications are reading/appending to streams.

If your expected version is not the current version you receive an HTTP status code of 400.

::: warning
See the idempotence section below, if you post the same event twice it is idempotent and won't return a version error.
:::

First append an event to a stream, setting a version:

@[code](@httpapi/event-version.json)

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-version.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-version.sh)
:::
::::

If you now append to the stream with the incorrect version, you receive an HTTP status code 400 error.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-wrong-version.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-wrong-version.sh)
:::
::::

There are special values you can use in the expected version header:

-   `-2` states that this append operation should never conflict and should **always** succeed.
-   `-1` states that the stream should not exist at the time of the appending (this append operation creates it).
-   `0` states that the stream should exist but should be empty.

## Idempotence

Appends to streams are idempotent based upon the `EventId` assigned in your post. If you were to re-run the last command it returns the same value again.

This is important behaviour as it's how you implement error handling. If you receive a timeout, broken connection, no answer, etc from your HTTP `POST` then it's your responsibility to retry the post. You must also keep the same UUID that you assigned to the event in the first `POST`.

If you are using the expected version parameter with your post, then EventStoreDB is 100% idempotent. If you use `-2` as your expected version value, EventStoreDB does its best to keep events idempotent but cannot assure that everything is fully idempotent and you end up in 'at-least-once' messaging. [Read this guide](optimistic-concurrency-and-idempotence.md) for more details on idempotence.
