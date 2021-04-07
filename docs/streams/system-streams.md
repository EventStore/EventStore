# System events and streams

## `$persistentSubscriptionConfig`

`$persistentSubscriptionConfig` is a special paged stream that contains all configuration events, for all persistent subscriptions. It uses the `PersistentConfig` system event type, which records a configuration event. The event data contains:

- `version`: Version of event data
- `updated`: Updated date
- `updatedBy`: User who updated configuration
- `maxCount`: The number of configuration events to save
- `entries`: Configuration items set by event.

## `$all`

`$all` is a special paged stream for all events. You can use the same paged form of reading described above to read all events for a node by pointing the stream at _/streams/\$all_. As it's a stream like any other, you can perform all operations, except posting to it.

## `$settings`

The `$settings` stream has a special ACL used as the default ACL. This stream controls the default ACL for streams without an ACL and also controls who can create streams in the system.

Learn more about the default ACL in the [access control lists](../security/acl.md#default-acl) documentation.

## `$stats`

EventStoreDB has debug and statistics information available about a cluster in the `$stats` stream, find out more in [the stats guide](../diagnostics/stats.md).

## `$scavenges`

`$scavenges` is a special paged stream for all scavenge related events. It uses the following system event types:

- `$scavengeIndexInitialized`: An event that records the initialisation of the scavenge index.
- `$scavengeStarted`: An event that records the beginning of a scavenge event, the event data contains:

  - `scavengeId`: Scavenge event ID
  - `nodeEndpoint`: Node address

- `$scavengeCompleted`: An event that records the completion of a scavenge event, the event data contains:
  - `scavengeId`: Scavenge event ID
  - `nodeEndpoint`: Node address
  - `result`: `Success`, `Failed`, `Stopped`
  - `error`: Error details
  - `timeTaken`: Time taken for the scavenge event in milliseconds
  - `spaceSaved`: Space saved by scavenge event in bytes
