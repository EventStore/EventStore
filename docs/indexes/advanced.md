# Indexing in depth

For general operation of EventStoreDB the following information is not critical but useful for developers wanting to make changes in the indexing subsystem and for understanding crash recovery and tuning scenarios.

## Index map files

_Indexmap_ files are text files made up of line delimited values. The line delimiter varies based on operating system, so while you can consider _indexmap_ files valid when transferred between operating systems, if you make changes to fix an issue (for example, disk corruption) it is best to make them on the same operating system as the cluster.

The _indexmap_ structure is as follows:

- `hash` - an md5 hash of the rest of the file
- `version` - the version of the _indexmap_ file
- `checkpoint` - the maximum prepare/commit position of the persisted _ptables_
- `maxAutoMergeLevel` - either the value of `MaxAutoMergeLevel` or `int32.MaxValue` if it was not set. This is primarily used to detect increases in `MaxAutoMergeLevel`, which is not supported.
- `ptable`,`level`,`index`- List of all the _ptables_ used by this index map with the level of the _ptable_ and it's order.

EventStoreDB writes _indexmap_ files to a temporary file and then deletes the original and renames the temporary file. EventStoreDB attempts this 5 times before failing. Because of the order, this operation can only fail if there is an issue with the underlying file system or the disk is full. This is a 2 phase process, and in the unlikely event of a crash during this process, EventStoreDB recovers by rebuilding the indexes using the same process used if it detects corrupted files during startup.

## Writing and merging of index files

Merging _ptables_, updating the _indexmap_ and persisting _memtable_ operations happen on a background thread. These operations are performed on a single thread with any additional operations queued and completed later. EventStoreDB runs these operations on a thread pool thread rather than a dedicated thread. Generally there is only one operation queued at a time, but if merging to _ptables_ at one level causes 2 tables to be available at the next level, then the next merge operation is immediately queued. While merge operations are in progress, if EventStoreDB is appending large numbers of events, it may queue 1 or more _memtables_ for persistence. The number of pending operations is logged.

For safety _ptables_ EventStoreDB is currently merging are only deleted after the new _ptable_ has persisted and the _indexmap_  updated. In the event of a crash, EventStoreDB recovers by deleting any files not in the _indexmap_ and reindexing from the prepare/commit position stored in the _indexmap_ file.

## Manual Merging

If you have set the maximum level ([`MaxAutoMergeIndexLevel`](./configuration.md#auto-merge-index-level)) for automatically merging indexes, then you need to trigger merging indexes above this level manually by using the `/admin/mergeindexes` endpoint, or the es-cli tool that is available with commercial support.

Triggering a manual merge causes EventStoreDB to merge all tables that have a level equal to the maximum merge level or above into a single table. If there is only 1 table at the maximum level or above, no merge is performed.

## Stream Existence Filter

The _Stream Existence Filter_ is a pair of files in the index directory.

- `/index/stream-existence/streamExistenceFilter.chk`
- `/index/stream-existence/streamExistenceFilter.dat`

It is made up of a persisted Bloom filter (the `.dat` file), and a checkpoint (the `.chk` file) that records where in the log the filter has processed up to. As per the documented backup procedure, the checkpoint needs to be backed up before the dat file.

The Stream Existence Filter is used when reading and writing to quickly determine whether a stream _might exist_ or _definitely does not exist_. If a stream definitely does not exist then EventStoreDB can skip looking through the index to find information about it. This is particularly useful when creating new streams (i.e. writing to streams that did not previously exist).

Additional information can be found in this [`blog post`](https://www.eventstore.com/blog/bloom-filters).
