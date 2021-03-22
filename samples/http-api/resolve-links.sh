#region curl
curl -i http://127.0.0.1:2113/streams/shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167/0 \
    -H "accept:application/vnd.eventstore.atom+json"
    -H "ES-ResolveLinkTos: true"
#endregion curl

#region response
HTTP/1.1 200 OK
Access-Control-Allow-Methods: GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
Cache-Control: max-age=31536000, public
Vary: Accept
Content-Type: application/vnd.eventstore.atom+json; charset=utf-8
Server: Mono-HTTPAPI/1.0
Date: Tue, 28 Aug 2018 13:13:49 GMT
Content-Length: 918
Keep-Alive: timeout=15,max=100

{
  "title": "0@shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167",
  "id": "http://127.0.0.1:2113/streams/shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167/0",
  "updated": "2018-08-28T12:56:15.263731Z",
  "author": {
    "name": "EventStore"
  },
  "summary": "ItemAdded",
  "content": {
    "eventStreamId": "shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167",
    "eventNumber": 0,
    "eventType": "ItemAdded",
    "eventId": "b989fe21-9469-4017-8d71-9820b8dd1167",
    "data": {
      "Description": "Xbox One Elite (Console)"
    },
    "metadata": {
      "TimeStamp": "2016-12-23T10:00:00.9225401+01:00"
    }
  },
  "links": [
    {
      "uri": "http://127.0.0.1:2113/streams/shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167/0",
      "relation": "edit"
    },
    {
      "uri": "http://127.0.0.1:2113/streams/shoppingCart-b989fe21-9469-4017-8d71-9820b8dd1167/0",
      "relation": "alternate"
    }
  ]
}
#endregion response
