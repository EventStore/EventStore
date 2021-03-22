#region curl
curl -i -d "@paging-events.json" "http://127.0.0.1:2113/streams/alphabet" -H "Content-Type:application/vnd.eventstore.events+json"
#endregion curl

#region response
HTTP/1.1 100 Continue

HTTP/1.1 201 Created
Access-Control-Allow-Methods: POST, DELETE, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
Location: http://127.0.0.1:2113/streams/alphabet/0
Content-Type: text/plain; charset=utf-8
Server: Mono-HTTPAPI/1.0
Date: Tue, 21 Aug 2018 09:53:46 GMT
Content-Length: 0
Keep-Alive: timeout=15,max=100
#endregion response
