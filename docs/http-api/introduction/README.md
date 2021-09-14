# Overview

EventStoreDB provides a native interface of AtomPub over HTTP. AtomPub is a RESTful protocol that can reuse many existing components, for example reverse proxies and a client's native HTTP caching. Since events stored in EventStoreDB are immutable, cache expiration can be infinite. EventStoreDB leverages content type negotiation and you can access appropriately serialised events can as JSON or XML according to the request headers.

## Compatibility with AtomPub

EventStoreDB v5 is fully compatible with the [1.0 version of the Atom Protocol](https://datatracker.ietf.org/doc/html/rfc4287). EventStoreDB adds extensions to the protocol, such as headers for control and custom `rel` links.

::: warning
The latest versions of EventStoreDB (v20+) have the AtomPub protocol disabled by default. We do not advise creating new applications using AtomPub as we plan to deprecate it. Please explore our new gRPC protocol available in v20. It provides more reliable real-time event streaming with wide range of platforms and language supported.
:::

### Existing implementations

Many environments have already implemented the AtomPub protocol, which simplifies the process.

| Library     | Description                                                       |
| ----------- | ----------------------------------------------------------------- |
| NET (BCL)   | `System.ServiceModel.SyndicationServices`                         |
| JVM         | <http://java-source.net/open-source/rss-rdf-tools>                |
| PHP         | <http://simplepie.org/>                                           |
| Ruby        | <https://github.com/cardmagic/simple-rss>                         |
| Clojure     | <https://github.com/scsibug/feedparser-clj>                       |
| Python      | <http://code.google.com/p/feedparser/>                            |
| node.js     | <https://github.com/danmactough/node-feedparser>                  |

::: warning
These are not officially supported by EventStoreDB.
:::

### Content types

The preferred way of determining which content type responses EventStoreDB serves is to set the `Accept` header on the request. As some clients do not deal well with HTTP headers when caching, appending a format parameter to the URL is also supported, for example, `?format=xml`.

The accepted content types for POST requests are:

- `application/xml`
- `application/vnd.eventstore.events+xml`
- `application/json`
- `application/vnd.eventstore.events+json`
- `text/xml`

The accepted content types for GET requests are:

- `application/xml`
- `application/atom+xml`
- `application/json`
- `application/vnd.eventstore.atom+json`
- `text/xml`
- `text/html`
- `application/vnd.eventstore.streamdesc+json`
