curl -i -d @override-default.json http://127.0.0.1:2113/streams/%24settings/metadata /
    --user admin:changeit /
    -H "Content-Type: application/vnd.eventstore.events+json"
