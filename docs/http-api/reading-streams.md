# Reading streams and events

## Reading a stream

EventStoreDB exposes streams as a resource located at `http(s)://{yourdomain.com}:{port}/streams/{stream}`. If you issue a simple `GET` request to this resource, you receive a standard AtomFeed document as a response.

:::: code-group
::: code Request

<<< @/samples/http-api/read-stream.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-stream.sh#response
:::
::::

## Reading an event from a stream

The feed has one item in it, and if there are more than one, then items are sorted from newest to oldest.

For each entry, there are a series of links to the actual events, [we cover embedding data into a stream later](#embedding-data-into-streams-in-json-format). To `GET` an event, follow the `alternate` link and set your `Accept` headers to the mime type you would like the event in.

The accepted content types for `GET` requests are:

- `application/xml`
- `application/atom+xml`
- `application/json`
- `application/vnd.eventstore.atom+json`
- `text/xml`
- `text/html`

The non-atom version of the event has fewer details about the event.

:::: code-group
::: code Request
<<< @/samples/http-api/read-event.sh#curl
:::
::: code Response
<<< @/samples/http-api/read-event.sh#response
:::
::::

## Feed paging

The next step in understanding how to read a stream is the `first`/`last`/`previous`/`next` links within a stream. EventStoreDB supplies these links, so you can read through a stream, and they follow the pattern defined in [RFC 5005](http://tools.ietf.org/html/rfc5005).

In the example above the server returned the following `links` as part of its result:

:::: code-group
::: code Request
<<< @/samples/http-api/read-stream.sh#curl
:::
::: code Response
<<< @/samples/http-api/read-stream.sh#response
:::
::::

This shows that there is not a `next` URL as all the information is in this request and that the URL requested is the first link. When dealing with these URLs, there are two ways of reading the data in the stream.

- You `GET` the `last` link and move backwards following `previous` links, or
- You `GET` the `first` link and follow the `next` links, and the final item will not have a `next` link.

If you want to follow a live stream, then you keep following the `previous` links. When you reach the end of a stream, you receive an empty document with no entries or `previous` link. You then continue polling this URI (in the future a document will appear). You can see this by trying the `previous` link from the above feed.

:::: code-group
::: code Request

<<< @/samples/http-api/read-stream-forwards.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-stream-forwards.sh#response
:::
::::

When parsing an atom subscription, the IDs of events always stay the same. This is important for figuring out if you are referring to the same event.

## Paging through events

Let's now try an example with more than a single page. First create the multiple events:

:::: code-group
::: code Request

<<< @/samples/http-api/append-paging-events.sh#curl
:::
::: code Response

<<< @/samples/http-api/append-paging-events.sh#response
:::
::::

If you request the stream of events, you see a series of links above the events:

:::: code-group
::: code Request

<<< @/samples/http-api/request-paging-events.sh#curl
:::
::: code Response

<<< @/samples/http-api/request-paging-events.sh#response
:::
::::

Using the links in the stream of events, you can traverse through all the events in the stream by going to the `last` URL and following `previous` links, or by following `next` links from the `first` link.

For example, if you request the `last` link from above:

:::: code-group
::: code Request

<<< @/samples/http-api/request-last-link.sh#curl
:::
::: code Response

<<< @/samples/http-api/request-last-link.sh#response
:::
::::

You then follow `previous` links until you are back to the head of the stream, where you can continue reading events in real time by polling the `previous` link.

::: tip
All links except the head link are fully cacheable as you can see in the HTTP header `Cache-Control: max-age=31536000, public`. This is important when discussing intermediaries and performance as you commonly replay a stream from storage. You should **never** bookmark links aside from the head of the stream resource, and always follow links. We may in the future change how internal links work, and bookmarking links other than the head may break.
:::

## Reading all events

`$all` is a special paged stream for all events. You can use the same paged form of reading described above to read all events for a node by pointing the stream at _/streams/\$all_. As it's a stream like any other, you can perform all operations, except posting to it.

::: tip
To access the `$all` stream, you must use admin details. Find more information on the [security](security.md) page.
:::

:::: code-group
::: code Request

<<< @/samples/http-api/read-all-events.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-all-events.sh#response
:::
::::

## Conditional GETs

The head link supports conditional `GET`s with the use of [ETAGS](http://en.wikipedia.org/wiki/HTTP_ETag), a well-known HTTP construct. You can include the ETAG of your last request and issue a conditional `GET` to the server. If nothing has changed, it won't return the full feed. For example the earlier response has an ETAG:

<<< @/samples/http-api/request-paging-events.sh#responseHeader{8}

You can use this in your next request when polling the stream for changes by putting it in the `If-None-Match` header. This tells the server to check if the response is the one you already know and returning a '304 not modified' response. If the tags have changed, the server returns a '200 OK' response. You can use this method to optimise your application by not sending large streams if there are no changes.

:::: code-group
::: code Request

<<< @/samples/http-api/request-etag.sh#curl
:::
::: code Response

<<< @/samples/http-api/request-etag.sh#response
:::
::::

::: tip
You create Etags using the version of the stream and the media type of the stream you are reading. You can't read an Etag from a stream in one media type and use it with another media type.
:::

## Embedding data into streams in JSON format

So far in this guide, the feeds returned have contained links that refer to the actual event data. This is normally a preferable mechanism for several reasons:

- They can be in a different media type than the feed, and you can negotiate them separately from the feed itself (for example, the feed in JSON, the event in XML). You can cache the event data separately from the feed, and you can point it to different feeds. If you use a `linkTo()` in your [projection](projections/README.md) this is what happens in your atom feeds.
- If you are using JSON, you can embed the events into the atom feed events. This can help cut down on the number of requests in some situations, but the messages are larger.

There are ways of embedding events and further metadata into your stream by using the `embed` parameter.

### Rich embed mode

The `rich` embed mode returns more properties about the event (`eventtype`, `streamid`, `position`, and so on) as you can see in the following request.

:::: code-group
::: code Request

<<< @/samples/http-api/read-stream-rich.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-stream-rich.sh#response
:::
::::

### Body embed mode

The `body` embed mode returns the JSON/XML body of the events into the feed as well, depending on the type of the feed. You can see this in the request below:

:::: code-group
::: code Request

<<< @/samples/http-api/read-stream-body.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-stream-body.sh#response
:::
::::

#### Variants of body embed mode

Two other modes are variants of `body`:

- `PrettyBody` tries to reformat the JSON to make it "pretty to read".
- `TryHarder` works harder to try to parse and reformat the JSON from an event to return it in the feed. These do not include further information and are focused on how the feed looks.

## Embedding data into streams in XML format

The XML format embeds no additional data, as only JSON supports embedding.

:::: code-group
::: code Request

<<< @/samples/http-api/read-stream-xml.sh#curl
:::
::: code Response

<<< @/samples/http-api/read-stream-xml.sh#response
:::
::::
