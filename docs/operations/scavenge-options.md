# Scavenging options

Below you can find some options that change the way how scavenging works on the server node.

## Disable scavenge merging

EventStoreDB might decide to merge indexes, depending on the [server settings](../indexes/advanced.md#writing-and-merging-of-index-files). The index merge is IO intensive operation, as well as scavenging, so you might not want them to run at the same time. Also, the scavenge operation rearranges chunks, so indexes might change too. You can instruct EventStoreDB not to merge indexes when the scavenging is running.
 
| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-scavenge-merging` |
| YAML                 | `DisableScavengeMerging` |
| Environment variable | `EVENTSTORE_DISABLE_SCAVENGE_MERGING` | 

**Default**: `false`, so EventStoreDB might run the index merge and scavenge at the same time.

## Scavenge history

Each scavenge operation gets an id and creates a stream. You might want to look at these streams to see the scavenge history, see how much time each operation took and how much disk space was reclaimed. However, you might not want to keep this history forever. Use the following option to limit how long the scavenge history stays in the database:

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--scavenge-history-max-age` |
| YAML                 | `ScavengeHistoryMaxAge` |
| Environment variable | `EVENTSTORE_SCAVENGE_HISTORY_MAX_AGE` | 

**Default**: `30` (days)

## Always keep scavenged

Scavenging aims to save disk space. Therefore, if the scavenging process finds out that the new chunk is for some reason larger or has the same size as the old chunk, it won't replace the old chunk because there's no disk space to save.

Such behaviour, however, is not always desirable. For example, you might want to be sure that events from deleted streams are removed from the disk. It is especially relevant in the context of personal data deletion.

When the `AlwaysKeepScavenged` option is set to `true`, EventStoreDB would replace the old chunk with the new one unconditionally, giving you guarantee that all the deleted events in the scavenged chunk actually disappear. 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--always-keep-scavenged` |
| YAML                 | `AlwaysKeepScavenged` |
| Environment variable | `EVENTSTORE_ALWAYS_KEEP_SCAVENGED` | 

**Default**: `false`

EventStoreDB will always keep one event in the stream even if the stream was deleted, to indicate the stream existence and the last event version. That last event in the deleted stream will still be there even with `AlwaysKeepScavenged` option enabled. Read more about [deleting streams](../streams/deleting-streams-and-events.md) to avoid keeping sensitive information in the database, which you otherwise would consider as deleted.

## Ignore hard delete

When you [delete a stream](../streams/deleting-streams-and-events.md), you can use either a soft delete or hard delete. When using hard delete, the stream gets closed with a tombstone event. Such an event tells the database that the stream cannot be reopened, so any attempt to append to the hard-deleted stream will fail. The tombstone event doesn't get scavenged.

You can override this behaviour and tell EventStoreDB that you want to delete all the traces of hard-deleted streams too, using the option specified below. After a scavenge operation runs, all hard-deleted streams will be open for writing new events again.

::: warning Active chunk
If you hard-delete a stream in the current chunk, it will remain hard-deleted even with this option enabled. It's because the active chunk won't be affected by the scavenge.
:::

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--unsafe-ignore-hard-delete` |
| YAML                 | `UnsafeIgnoreHardDelete` |
| Environment variable | `EVENTSTORE_UNSAFE_IGNORE_HARD_DELETE` | 

**Default**: `false`

::: warning Unsafe
Setting this option to `true` disables hard deletes and allows clients to append to deleted streams. For that reason, the option is considered unsafe and should be used with caution.
:::
