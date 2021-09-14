# Requires Master

When running in a clustered environment there are times when you only want an operation to happen on the current leader node. A client can fetch information in an eventually consistent fashion by communicating with the servers. The TCP client included with the multi-node version does this.

Over HTTP the `RequiresMaster` header tells the node that it is not allowed to serve a read or forward a write request. If the node is the leader everything works as normal, if it isn't it responds with a `307` temporary redirect to the leader.

Run the below on the master:

:::: code-group
::: code-group-item Request
```bash
curl -i "http://127.0.0.1:32004/streams/newstream" \
    -H "ES-RequireMaster: True"
```
:::
::: code-group-item Response
```json
HTTP/1.1 200 OK
Cache-Control: max-age=0, no-cache, must-revalidate
Content-Length: 1296
Content-Type: application/vnd.eventstore.atom+json; charset: utf-8
ETag: "0;-2060438500"
Vary: Accept
Server: Microsoft-HTTPAPI/2.0
Access-Control-Allow-Methods: POST, DELETE, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-PINGOTHER
Access-Control-Allow-Origin: *
Date: Thu, 27 Jun 2013 14:48:37 GMT

{
  "title": "Event stream 'stream'",
  "id": "http://127.0.0.1:32004/streams/stream",
  "updated": "2013-06-27T14:48:15.2596358Z",
  "streamId": "stream",
  "author": {
    "name": "EventStore"
  },
  "links": [
    {
      "uri": "http://127.0.0.1:32004/streams/stream",
      "relation": "self"
    },
    {
      "uri": "http://127.0.0.1:32004/streams/stream/head/backward/20",
      "relation": "first"
    },
    {
      "uri": "http://127.0.0.1:32004/streams/stream/0/forward/20",
      "relation": "last"
    },
    {
      "uri": "http://127.0.0.1:32004/streams/stream/1/forward/20",
      "relation": "previous"
    },
    {
      "uri": "http://127.0.0.1:32004/streams/stream/metadata",
      "relation": "metadata"
    }
  ],
  "entries": [
    {
      "title": "0@stream",
      "id": "http://127.0.0.1:32004/streams/stream/0",
      "updated": "2013-06-27T14:48:15.2596358Z",
      "author": {
        "name": "EventStore"
      },
      "summary": "TakeSomeSpaceEvent",
      "links": [
        {
          "uri": "http://127.0.0.1:32004/streams/stream/0",
          "relation": "edit"
        },
        {
          "uri": "http://127.0.0.1:32004/streams/stream/0",
          "relation": "alternate"
        }
      ]
    }
  ]
}
```
:::
::::

Run the following on any other node:

:::: code-group
::: code-group-item Request
```bash
curl -i "http://127.0.0.1:31004/streams/newstream" \
    -H "ES-RequireMaster: True"
```
:::
::: code-group-item Response
```http
HTTP/1.1 307 Temporary Redirect
Content-Length: 0
Content-Type: text/plain; charset: utf-8
Location: http://127.0.0.1:32004/streams/stream
Server: Microsoft-HTTPAPI/2.0
Access-Control-Allow-Methods: POST, DELETE, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-PINGOTHER
Access-Control-Allow-Origin: *
Date: Thu, 27 Jun 2013 14:48:28 GMT
```
:::
::::
