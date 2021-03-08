#region curl
curl -i -d @override-default.json http://127.0.0.1:2113/streams/%24settings/metadata /
    --user admin:changeit /
    -H "Content-Type: application/vnd.eventstore.events+json"
#endregion curl

#region response
HTTP/1.1 201 Created
Access-Control-Allow-Methods: GET, POST, GET, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
Location: http://127.0.0.1:2113/streams/%24%24%24users/0
Content-Type: text/plain; charset=utf-8
Server: Mono-HTTPAPI/1.0
Date: Thu, 23 Aug 2018 10:35:19 GMT
Content-Length: 0
Keep-Alive: timeout=15,max=100
#region response
