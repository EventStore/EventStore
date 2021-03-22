curl -i -X PUT -d $'{"startFrom": 0,"resolveLinktos": false}' \
    http://localhost:2113/subscriptions/newstream/examplegroup \
    -u admin:changeit -H "Content-Type: application/json"
