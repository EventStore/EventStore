# Deleting streams and events

Metadata in EventStoreDB defines whether an event is deleted or not. You can use [stream metadata](metadata-and-reserved-names.md) such as `TruncateBefore`, `MaxAge` and `MaxCount` to filter events considered deleted. When reading a stream, the index checks the stream's metadata to determine whether any of its events have been deleted.

You cannot delete events from the middle of the stream. EventStoreDB only allows to _truncate_ the stream.

When you delete a stream, you can use either a soft delete or hard delete. When a stream is soft-deleted, all events from the stream get scavenged during the next scavenging run. It means that you can reopen the stream by appending to it again. When using hard delete, the stream gets closed with a tombstone event. Such an event tells the database that the stream cannot be reopened, so any attempt to append to the hard-deleted stream will fail. The tombstone event doesn't get scavenged.

The `$all` stream bypasses the index, meaning that it does not check the metadata to determine whether events exist or not. As such, events that have been deleted are still be readable until a scavenge has removed them. There are requirements for a scavenge to successfully remove events, for more information about this, read the [scavenging guide](../scavenge/).

EventStoreDB will always keep one event in the stream even if the stream was deleted, to indicate the stream existence and the last event version. Therefore, we advise you to append a specific event like `StreamDeleted` and then set the max count to one to keep the stream or delete the stream. Keep that in mind when deleting streams that contain sensitive information that you really want to remove without a trace.

## Soft delete and `TruncateBefore`

`TruncateBefore` and `$tb` considers any event with an event number lower than its value as deleted.
For example, if you had the following events in a stream :

```
0@test-stream
1@test-stream
2@test-stream
3@test-stream
```

If you set the `TruncateBefore` or `$tb` value to 3, a read of the stream would result in only reading the last event:

```
3@test-stream
```

A **soft delete** makes use of `TruncateBefore` and `$tb`. When you delete a stream, its `TruncateBefore` or `$tb` is set to the [max long/Int64 value](https://docs.microsoft.com/en-us/dotnet/api/system.int64.maxvalue?view=net-5.0): 9223372036854775807. When you read a soft deleted stream, the read returns a `StreamNotFound` or `404` result.
After deleting the stream, you are able to append to it again, continuing from where it left off.

For example, if you soft deleted the above example stream, the `TruncateBefore` or `$tb` is set to 9223372036854775807. If you were to append to the stream again, the next event is appended with event number 4. Only events from event number 4 (last stream revision before deleting, incremented by one) onwards are visible when you read this stream.

## Max count and Max age

**Max count** (`$maxCount` and `MaxCount`) limits the number of events that you can read from a stream. If you try to read a stream that has a max count of 5, you are only able to read the last 5 events, regardless of how many events are in the stream.

**Max age** (`$maxAge` and `MaxAge`) specifies the number of seconds an event can live for. The age is calculated at the time of the read. So if you read a stream with a `MaxAge` of 3 minutes and one of the events in the stream has existed for 4 minutes at the time of the read, it is not returned.

## Hard delete

A **hard delete** appends a `tombstone` event to the stream, permanently deleting it. You cannot recreate the stream, or append to it again. Tombstone events are appended with the event type `$streamDeleted`. When you read a hard deleted stream, the read returns a `StreamDeleted` or `410` result.

The events in the deleted stream are liable to be removed in a scavenge, but the tombstone event remains.

A hard delete of a stream is permanent. You cannot append to the stream or recreate it. As such, you should generally soft delete streams unless you have a specific need to permanently delete the stream. On rare occasions when you need to re-open a hard-deleted stream, you can force EventStoreDB to scavenge it by using the [ignore hard deletes](../scavenge/options.md#ignore-hard-delete) option.

## Deleted events and projections

If you are intending on using projections and deleting streams, there are some things to take into consideration:

- Due to the nature of `$all`, projections using `fromAll` read any deleted events that have not been scavenged. They also receive any tombstone events from hard deletes.
- Projections that read from a specific stream receive that stream's metadata events. You can filter these out by ignoring events with an event type `$metadata`.
- System projections like [by category](../projections/system-projections.md#by-category) or [by event type](../projections/system-projections.md#by-event-type) projections produce new (link) events that are stored in the database in addition to the original event. When you delete the original events, then link events will remain in the projected streams, but their links won't be resolved (will have undefined value). You can ignore those events in the code logic.
