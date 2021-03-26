#region curl
curl -i -d "@event.json" "http://127.0.0.1:2113/streams/newstream" -H "Content-Type:application/json"
#endregion curl

#region response
HTTP/1.1 400 Must include an event type with the request either in body or as ES-EventType header.
Access-Control-Allow-Methods: POST, DELETE, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
Content-Type:
Server: Mono-HTTPAPI/1.0
Date: Tue, 24 Jul 2018 14:50:59 GMT
Content-Length: 0
Connection: close
#endregion response
