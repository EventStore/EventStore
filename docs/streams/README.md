# Event Streams

EventStoreDB is a database designed for storing events. In contrast with state-oriented databases that only keeps the latest version of the entity state, you can store each state change as a separate event.

Events are logically grouped into streams. They are the representation of the entities. All the entity state mutation ends up as the persisted event. 

Read more:
- [Metadata and reserved names.](./metadata-and-reserved-names.md)
- [Deleting streams and events.](./deleting-streams-and-events.md)
- [System events and streams.](./system-streams.md)
