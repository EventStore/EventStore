# Optional HTTP headers

<!-- TODO: Can Swagger replace this? And sub files -->

EventStoreDB supports custom HTTP headers for requests. The headers were previously in the form `X-ES-ExpectedVersion` but were changed to `ES-ExpectedVersion` in compliance with [RFC-6648](https://datatracker.ietf.org/doc/html/rfc6648).

The headers supported are:

| Header                                    | Description                                                                                        |
| ----------------------------------------- | -------------------------------------------------------------------------------------------------- |
| [ES-ExpectedVersion](expected-version.md) | The expected version of the stream (allows optimistic concurrency)                                 |
| [ES-ResolveLinkTo](resolve-linkto.md)     | Whether to resolve `linkTos` in stream                                                             |
| [ES-RequiresMaster](requires-master.md)   | Whether this operation needs to run on the master node                                             |
| [ES-TrustedAuth](/server/v5/security/trusted-intermediary.md) | Allows a trusted intermediary to handle authentication                                             |
| [ES-LongPoll](longpoll.md)                | Instructs the server to do a long poll operation on a stream read                                  |
| [ES-HardDelete](harddelete.md)            | Instructs the server to hard delete the stream when deleting as opposed to the default soft delete |
| [ES-EventType](eventtype.md)              | Instructs the server the event type associated to a posted body                                    |
| [ES-EventId](eventid.md)                  | Instructs the server the event id associated to a posted body                                      |
