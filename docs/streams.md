# Event streams

EventStoreDB is a database designed for storing events. In contrast with state-oriented databases that only
keeps the latest version of the entity state, you can store each state change as a separate event.

Events are logically grouped into streams. They are the representation of the entities. All the entity state
mutation ends up as the persisted event.

## Metadata and reserved names

### Stream metadata

Every stream in EventStoreDB has metadata stream associated with it, prefixed by `$$`, so the metadata stream
from a stream called `foo` is `$$foo`. EventStoreDB allows you to change some values in the metadata, and you
can write your own data into stream metadata that you can refer to in your code.

### Reserved names

All internal data used by EventStoreDB is prefixed with a `$` character (for example `$maxCount` on a stream's
metadata). Because of this you should not use names with a `$` prefix as event names, metadata keys, or stream
names, except where detailed below.

The supported internal settings are:

| Property name   | Description                                                                                                                                                                                                                                                                                                                                                                   |
|:----------------|:------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `$maxAge`       | Sets a sliding window based on dates. When data reaches a certain age it disappears automatically from the stream and is considered eligible for scavenging. This value is set as an integer representing the number of seconds. This value must be >= 1.                                                                                                                     |
| `$maxCount`     | Sets a sliding window based on the number of items in the stream. When data reaches a certain length it disappears automatically from the stream and is considered eligible for scavenging. This value is set as an integer representing the count of items. This value must be >= 1.                                                                                         |
| `$cacheControl` | This controls the cache of the head of a stream. Most URIs in a stream are infinitely cacheable but the head by default will not cache. It may be preferable in some situations to set a small amount of caching on the head to allow intermediaries to handle polls (say 10 seconds). The argument is an integer representing the seconds to cache. This value must be >= 1. |

::: tip 
If you set both `$maxAge` and `$maxCount` then events will become eligible for scavenging when either
criteria is met. For example, if you set `$maxAge` to 10 and `$maxCount` to 50,000, events will be marked as
eligible for scavenging after either 10 seconds, or 50,000 events, have passed. Deleted items will only be
removed once the scavenge process runs.
:::

Security access control lists are also included in the `$acl` section of the stream metadata.

| Property name | Description                                                 |
|:--------------|:------------------------------------------------------------|
| `$r`          | The list of users with read permissions                     |
| `$w`          | The list of users with write permissions                    |
| `$d`          | The list of users with delete permissions                   |
| `$mw`         | The list of users with write permissions to stream metadata |
| `$mr`         | The list of users with read permissions to stream metadata  |

You can find more details on access control lists can [here](./security.md#access-control-lists).

### Event metadata

Every event in EventStoreDB has metadata associated with it. EventStoreDB allows you to change some values in
the metadata, and you can write your own data into event metadata that you can refer to in your code.

All names starting with `$` are reserved space for internal use. The currently supported reserved internal
names are:

| Property name    | Description                                                        |
|:-----------------|:-------------------------------------------------------------------|
| `$correlationId` | The application level correlation ID associated with this message. |
| `$causationId`   | The application level causation ID associated with this message.   |

Projections honor both the `correlationId` and `causationId` patterns for any events it produces internally (
linkTo, emit, etc.).

## Deleting streams and events

Metadata in EventStoreDB defines whether an event is deleted or not. You can
use [stream metadata](#metadata-and-reserved-names) such as `TruncateBefore`, `MaxAge` and `MaxCount` to
filter events considered deleted. When reading a stream, the index checks the stream's metadata to determine
whether any of its events have been deleted.

You cannot delete events from the middle of the stream. EventStoreDB only allows to _truncate_ the stream.

When you delete a stream, you can use either a soft delete or hard delete. When a stream is soft-deleted, all
events from the stream get scavenged during the next scavenging run. It means that you can reopen the stream
by appending to it again. When using hard delete, the stream gets closed with a tombstone event. Such an event
tells the database that the stream cannot be reopened, so any attempt to append to the hard-deleted stream
will fail. The tombstone event doesn't get scavenged.

The `$all` stream bypasses the index, meaning that it does not check the metadata to determine whether events
exist or not. As such, events that have been deleted are still be readable until a scavenge has removed them.
There are requirements for a scavenge to successfully remove events, for more information about this, read
the [scavenging guide](operations.md#scavenging-events).

EventStoreDB will always keep one event in the stream even if the stream was deleted, to indicate the stream
existence and the last event version. Therefore, we advise you to append a specific event like `StreamDeleted`
and then set the max count to one to keep the stream or delete the stream. Keep that in mind when deleting
streams that contain sensitive information that you really want to remove without a trace.

### Soft delete and `TruncateBefore`

`TruncateBefore` and `$tb` considers any event with an event number lower than its value as deleted. For
example, if you had the following events in a stream :

```
0@test-stream
1@test-stream
2@test-stream
3@test-stream
```

If you set the `TruncateBefore` or `$tb` value to 3, a read of the stream would result in only reading the
last event:

```
3@test-stream
```

A **soft delete** makes use of `TruncateBefore` and `$tb`. When you delete a stream, its `TruncateBefore`
or `$tb` is set to
the [max long/Int64 value](https://docs.microsoft.com/en-us/dotnet/api/system.int64.maxvalue?view=net-5.0):
9223372036854775807. When you read a soft deleted stream, the read returns a `StreamNotFound` or `404` result.
After deleting the stream, you are able to append to it again, continuing from where it left off.

For example, if you soft deleted the above example stream, the `TruncateBefore` or `$tb` is set to
9223372036854775807. If you were to append to the stream again, the next event is appended with event number
4. Only events from event number 4 (last stream revision before deleting, incremented by one) onwards are
visible when you read this stream.

### Max count and Max age

**Max count** (`$maxCount` and `MaxCount`) limits the number of events that you can read from a stream. If you
try to read a stream that has a max count of 5, you are only able to read the last 5 events, regardless of how
many events are in the stream.

**Max age** (`$maxAge` and `MaxAge`) specifies the number of seconds an event can live for. The age is
calculated at the time of the read. So if you read a stream with a `MaxAge` of 3 minutes and one of the events
in the stream has existed for 4 minutes at the time of the read, it is not returned.

### Hard delete

A **hard delete** appends a `tombstone` event to the stream, permanently deleting it. You cannot recreate the
stream, or append to it again. Tombstone events are appended with the event type `$streamDeleted`. When you
read a hard deleted stream, the read returns a `StreamDeleted` or `410` result.

The events in the deleted stream are liable to be removed in a scavenge, but the tombstone event remains.

A hard delete of a stream is permanent. You cannot append to the stream or recreate it. As such, you should
generally soft delete streams unless you have a specific need to permanently delete the stream. On rare
occasions when you need to re-open a hard-deleted stream, you can force EventStoreDB to scavenge it by using
the [ignore hard deletes](operations.md#ignore-hard-delete) option.

### Deleted events and projections

If you are intending on using projections and deleting streams, there are some things to take into
consideration:

- Due to the nature of `$all`, projections using `fromAll` read any deleted events that have not been
  scavenged. They also receive any tombstone events from hard deletes.
- Projections that read from a specific stream receive that stream's metadata events. You can filter these out
  by ignoring events with an event type `$metadata`.
- System projections like [by category](projections.md#by-category)
  or [by event type](projections.md#by-event-type) projections produce new (link) events that are stored in
  the database in addition to the original event. When you delete the original events, then link events will
  remain in the projected streams, but their links won't be resolved (will have undefined value). You can
  ignore those events in the code logic.

## System events and streams

### `$persistentSubscriptionConfig`

`$persistentSubscriptionConfig` is a special paged stream that contains all configuration events, for all
persistent subscriptions. It uses the `PersistentConfig` system event type, which records a configuration
event. The event data contains:

- `version`: Version of event data
- `updated`: Updated date
- `updatedBy`: User who updated configuration
- `maxCount`: The number of configuration events to save
- `entries`: Configuration items set by event.

### `$all`

`$all` is a special paged stream for all events. You can use the same paged form of reading described above to
read all events for a node by pointing the stream at _/streams/\$all_. As it's a stream like any other, you
can perform all operations, except posting to it.

### `$settings`

The `$settings` stream has a special ACL used as the default ACL. This stream controls the default ACL for
streams without an ACL and also controls who can create streams in the system.

Learn more about the default ACL in the [access control lists](security.md#default-acl) documentation.

### `$stats`

EventStoreDB has debug and statistics information available about a cluster in the `$stats` stream, find out
more in [the stats guide](diagnostics.md#statistics).

### `$scavenges`

`$scavenges` is a special paged stream for all scavenge related events. It uses the following system event
types:

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
