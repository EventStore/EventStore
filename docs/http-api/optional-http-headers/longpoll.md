# LongPoll

You use the `ES-LongPoll` header to tell EventStoreDB that when on the head link of a stream and no data is available to wait a period of time to see if data becomes available.

You can use this to give lower latency for Atom clients instead of client initiated polling.

Instead of the client polling every 5 seconds to get data from the feed the client sends a request with `ES-LongPoll: 15`. This instructs EventStoreDB to wait for up to 15 seconds before returning with no result. The latency is therefore lowered from the poll interval to about 10ms from the time an event is appended until the time the HTTP connection is notified.

You can see the use of the `ES-LongPoll` header in the following cURL command.

First go to the head of the stream.

:::: code-group
::: code-group-item Request
@[code{curl}](../../samples/read-stream.sh)
:::
::: code-group-item Response
@[code{response}](../../samples/read-stream.sh)
:::
::::

Then fetch the previous `rel` link `http://127.0.0.1:2113/streams/newstream/2/forward/20` and try it. It returns an empty feed.

:::: code-group
::: code-group-item Request
@[code{curl}](../../samples/get-forward-link.sh)
:::
::: code-group-item Response
@[code{response}](../../samples/get-forward-link.sh)
:::
::::

The entries section is empty (there is no further data to provide). Now try the same URI with a long poll header.

@[code](../../samples/longpoll.sh)

If you do not insert any events into the stream while this is running it takes 10 seconds for the HTTP request to finish. If you append an event to the stream while its running you see the result for that request when you append the event.
