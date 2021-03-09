#region curl
curl -i -d {} -X POST http://localhost:2113/admin/scavenge -u "admin:changeit"
#endregion curl

#region response
HTTP/1.1 200 OK
Access-Control-Allow-Methods: POST, OPTIONS
Access-Control-Allow-Headers: Content-Type, X-Requested-With, X-Forwarded-Host, X-Forwarded-Prefix, X-PINGOTHER, Authorization, ES-LongPoll, ES-ExpectedVersion, ES-EventId, ES-EventType, ES-RequiresMaster, ES-HardDelete, ES-ResolveLinkTos
Access-Control-Allow-Origin: *
Access-Control-Expose-Headers: Location, ES-Position, ES-CurrentVersion
Content-Type:
Server: Mono-HTTPAPI/1.0
Date: Wed, 19 Sep 2018 10:25:55 GMT
Content-Length: 0
Keep-Alive: timeout=15,max=100
#endregion response
