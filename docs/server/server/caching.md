# HTTP caching

::: tip
This article is about caching static resources for HTTP API. It does not affect the server performance directly and cannot be used with TCP clients.
:::

Most of the URIs that EventStoreDB emits are immutable (including the UI and Atom Feeds).

An Atom feed has a URI that represents an event, e.g., `/streams/foo/0`, representing 'event 0'. The data for event 0 never changes. If this stream is open to public reads then the URI is set to be 'cachable' for long periods of time.

You can see a similar example in reading a feed. If a stream has 50 events in it, the feed page `20/forward/10` never changes, it will always be events 20-30. Internally EventStoreDB controls serving the right URIs by using `rel` links with feeds (for example `prev`/`next`).

This caching behaviour is great for performance in a production environment and we recommended you use it, but in a developer environment it can become confusing.

For example, what happens if you started a database, wrote `/streams/foo/0` and performed a `GET` request? The `GET` request is cachable and now in your cache. Since this is a development environment, you shutdown EventStoreDB and delete the database. You then restart EventStoreDB and append a different event to `/streams/foo/0`. You open your browser and inspect the `/streams/foo/0` stream, and you see the event appended before you deleted the database.

To avoid this during development it's best to run EventStoreDB with the `--disable-http-caching` command line option. This disables all caching and solve the issue.

The option can be set as follows:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-http-caching` |
| YAML                 | `DisableHttpCaching` |
| Environment variable | `EVENTSTORE_DISABLE_HTTP_CACHING` | 

**Default**: `false`, so the HTTP caching is **enabled** by default.
