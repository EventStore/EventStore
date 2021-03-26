# HardDelete

The `ES-HardDelete` header controls deleting a stream. By default EventStoreDB soft deletes a stream allowing you to later reuse that stream. If you set the `ES-HardDelete` header EventStoreDB permanently deletes the stream.

:::: code-group
::: code Request
<<< @/samples/http-api/delete-stream/hard-delete-stream.sh#curl
:::
::: code Response
<<< @/samples/http-api/delete-stream/hard-delete-stream.sh#response
:::
::::

This changes the general behaviour from returning a `404` and the stream to be recreated (soft-delete) to the stream now return a `410 Deleted` response.
