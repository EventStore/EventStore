#region curl
curl -i -d @event-version.json "http://127.0.0.1:2113/streams/newstream" \
    -H "Content-Type:application/vnd.eventstore.events+json" \
    -H "ES-ExpectedVersion: 3"
#endregion curl

#region response
HTTP/1.1 400 Wrong expected EventNumber
Access-Control-Allow-Methods: POST, DELETE, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
ES-CurrentVersion: 0
Content-Type: text/plain; charset=utf-8
Server: Mono-HTTPAPI/1.0
Date: Tue, 14 Aug 2018 14:08:44 GMT
Content-Length: 0
Connection: close
#endregion response
