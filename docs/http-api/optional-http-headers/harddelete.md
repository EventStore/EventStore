# HardDelete

The `ES-HardDelete` header controls deleting a stream. By default EventStoreDB soft deletes a stream allowing you to later reuse that stream. If you set the `ES-HardDelete` header EventStoreDB permanently deletes the stream.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/delete-stream/hard-delete-stream.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/delete-stream/hard-delete-stream.sh)
:::
::::

This changes the general behaviour from returning a `404` and the stream to be recreated (soft-delete) to the stream now return a `410 Deleted` response.
