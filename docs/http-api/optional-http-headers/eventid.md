# EventID

When you append to a stream and don't use the `application/vnd.eventstore.events+json/+xml` media type, you need to specify an event ID with the event you post. This is not required with the custom media type as it is specified within the format (there is an `EventId` on each entry in the format). EventStoreDB uses `EventId` for impotency.

You can include an event ID on an event by specifying this header.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-to-new-stream.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-to-new-stream.sh)
:::
::::

If you don't add an `ES-EventId` header on an append where the body is considered the actual event (e.g., not using `application/vnd.eventstore.events+json/+xml`) EventStoreDB generates a unique identifier for you and redirects you to an idempotent URI where you can post your event. If you can create a UUID then you shouldn't use this feature, but it's useful when you cannot create a UUID.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/append-event-no-id.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/append-event-no-id.sh)
:::
::::

EventStoreDB returned a `307 Temporary Redirect` with a location header that points to a generated URI that is idempotent for purposes of retrying the post.
