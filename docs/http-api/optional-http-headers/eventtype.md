# EventType

When you append to a stream and don't the `application/vnd.eventstore.events+json/+xml` media type you must specify an event type with the event that you are posting. This isn't required with the custom media type as it's specified within the format itself.

You use the `ES-EventType` header as follows.

:::: code-group
::: code Request
<<< @/docs/server/v5/http-api/sample-code/append-event-to-new-stream.sh#curl
:::
::: code Response
<<< @/docs/server/v5/http-api/sample-code/append-event-to-new-stream.sh#response
:::
::::

If you view the event in the UI or with cURL it has the `EventType` of `SomeEvent`:

<!-- TODO: Does this make sense? If I can't use the custom media type -->

:::: code-group
::: code Request
<<< @/docs/server/v5/http-api/sample-code/read-event.sh#curl
:::
::: code Response
<<< @/docs/server/v5/http-api/sample-code/read-event.sh#response
:::
::::
