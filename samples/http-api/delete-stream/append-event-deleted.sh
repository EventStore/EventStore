curl -i -d "@event-append.json" "http://127.0.0.1:2113/streams/newstream" \
    -H "Content-Type:application/vnd.eventstore.events+json" \
    -H "ES-EventType: SomeEvent"
