# Deleting a stream

## Soft deleting

To delete a stream over the Atom interface, issue a `DELETE` request to the resource.

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/delete-stream.sh
:::
::: code Response
<<< @/samples/http-api/delete-stream/delete-stream-response.http
:::
::::

By default when you delete a stream, EventStoreDB soft deletes it. This means you can recreate it later by setting the `$tb` metadata section in the stream. If you try to `GET` a soft deleted stream you receive a 404 response:

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/get-deleted-stream.sh
:::
::: code Response
<<< @/samples/http-api/delete-stream/get-deleted-stream-response.http
:::
::::

You can recreate the stream by appending new events to it (like creating a new stream):

:::: code-group
::: code Request
<<< @/samples/http-api/append-event.sh#curl
:::
::: code Response
<<< @/samples/http-api/append-event.http
:::
::::

The version numbers do not start at zero but at where you soft deleted the stream from

## Hard deleting

You can hard delete a stream. To issue a permanent delete use the `ES-HardDelete` header.

::: warning
A hard delete is permanent and the stream is not removed during a scavenge. If you hard delete a stream, you cannot recreate the stream.
:::

Issue the `DELETE` as before but with the permanent delete header:

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/hard-delete-stream.sh
:::
::: code Response
<<< @/samples/http-api/delete-stream/hard-delete-stream.http
:::
::::

The stream is now permanently deleted, and now the response is a `410`.

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/get-deleted-stream.sh#curl
:::
::: code Response
<<< @/samples/http-api/delete-stream/get-deleted-stream-response.http
:::
::::

If you try to recreate the stream as in the above example you also receive a `410` response.

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/append-event-deleted.sh#curl
:::
::: code Response
<<< @/samples/http-api/delete-stream/append-event-deleted.sh#response
:::
::::
