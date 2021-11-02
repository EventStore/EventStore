# Configuration Options

The configuration options that effect indexing are:

| Option | What's it for |
| :----- | :------------ |
| [`Index`](#index-location) | Where the indexes are stored |
| [`MaxMemTableSize`](#memtable-size) | How many entries to have in memory before writing out to disk |
| [`IndexCacheDepth`](#index-cache-depth) | Sets the minimum number of midpoints to calculate for an index file |
| [`SkipIndexVerify`](#skip-index-verification) | Tells the server to not verify indexes on startup |
| [`MaxAutoMergeIndexLevel`](#auto-merge-index-level) | The maximum level of index file to merge automatically before manual merge |
| [`OptimizeIndexMerge`](#optimize-index-merge) | Bypasses the checking of file hashes of indexes during startup and after index merges (allows for faster startup and less disk pressure after merges) |
| [`StreamExistenceFilterSize`](#stream-existence-filter-size) | Size in bytes of the stream existence filter |
| [`IndexCacheSize`](#index-cache-size) | Maximum number of entries in each index LRU cache |
| [`UseIndexBloomFilters`](#use-index-bloom-filters) | Feature flag which can be used to disable the index Bloom filters |

Read more below to understand these options better.

## Index location

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--index` |
| YAML                 | `Index` |
| Environment variable | `EVENTSTORE_INDEX` | 

**Default**: data files location

`Index` effects the location of the index files. We recommend you place index files on a separate drive to avoid competition for IO between the data, index and log files.

## Memtable size

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--max-mem-table-size` |
| YAML                 | `MaxMemTableSize` |
| Environment variable | `EVENTSTORE_MAX_MEM_TABLE_SIZE` | 

**Default**: `1000000`

`MaxMemTableSize` effects disk IO when EventStoreDB writes files to disk, index seek time and database startup time. The default size is a good tradeoff between low disk IO and startup time. Increasing the `MaxMemTableSize` results in longer database startup time because a node has to read through the data files from the last position in the `indexmap` file and rebuild the in memory index table before it starts.

<!-- TODO: Polish a little more -->

Increasing `MaxMemTableSize` also decreases the number of times EventStoreDB writes index files to disk and how often it merges them together, which increases IO operations. It also reduces the number of seek operations when stream entries span multiple files as EventStoreDB needs to search each file for the stream entries. This affects streams written to over longer periods of time more than streams written to over a shorter time, where time is measured by the number of events created, not time passed. This is because streams written to over longer time periods are more likely to have entries in multiple index files.

## Index cache depth

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--index-cache-depth` |
| YAML                 | `IndexCacheDepth` |
| Environment variable | `EVENTSTORE_INDEX_CACHE_DEPTH` | 

**Default**: `16`

`IndexCacheDepth` effects the how many midpoints EventStoreDB calculates for an index file which effects file size slightly, but can effect lookup times significantly. Looking up a stream entry in a file requires a binary search on the midpoints to find the nearest midpoint, and then a seek through the entries to find the entry or entries that match. Increasing this value decreases the second part of the operation and increase the first for extremely large indexes.

**The default value of 16** results in files up to about 1.5GB in size being fully searchable through midpoints. After that a maximum distance between midpoints of 4096 bytes for the seek, which is buffered from disk, up to a maximum level of 2TB where the seek distance starts to grow. Reducing this value can relieve a small amount of memory pressure in highly constrained environments. Increasing it causes index files larger than 1.5GB, and less than 2TB to have more dense midpoint populations which means the binary search is not used for long before switching back to scanning the entries between. The maximum number of entries scanned in this way is `distance/24b`, so with the default setting and a 2TB index file this is approximately 170 entries. Most clusters should not need to change this setting.

## Skip index verification

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--skip-index-verify` |
| YAML                 | `SkipIndexVerify` |
| Environment variable | `EVENTSTORE_SKIP_INDEX_VERIFY` | 

**Default**: `false`

`SkipIndexVerify` skips reading and verification of index file hashes during startup. Instead of recalculating midpoints when EventStoreDB reads the file, it reads the midpoints directly from the footer of the index file. You can set `SkipIndexVerify` to `true` to reduce startup time in exchange for the acceptance of a small risk that the index file becomes corrupted. This corruption could lead to a failure if you read the corrupted entries, and a message saying the index needs to be rebuilt. You can safely disable this setting for ZFS on Linux as the filesystem takes care of file checksums.

In the event of corruption indexes will be rebuilt by reading through all the chunk files and recreating the indexes from scratch.

## Auto-merge index level

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--max-auto-merge-index-level` |
| YAML                 | `MaxAutoMergeIndexLevel` |
| Environment variable | `EVENTSTORE_MAX_AUTO_MERGE_INDEX_LEVEL` | 

**Default**: `2147483647`

`MaxAutoMergeIndexLevel` allows you to specify the maximum index file level to automatically merge. By default EventStoreDB merges all levels. Depending on the specification of the host running EventStoreDB, at some point index merges will use a large amount of disk IO.

For example:

> Merging 2 level 7 files results in at least 3072MB reads (2 \* 1536MB), and 3072MB writes while merging 2 level 8 files together results in at least 6144MB reads (2 \* 3072MB) and 6144MB writes. Setting `MaxAutoMergeLevel` to 7 allows all levels up to and including level 7 to be automatically merged, but to merge the level 8 files together, you need to trigger a manual merge. This manual merge allows better control over when these larger merges happen and which nodes they happen on. Due to the replication process, all nodes tend to merge at about the same time.

## Optimize index merge

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--optimize-index-merge` |
| YAML                 | `OptimizeIndexMerge` |
| Environment variable | `EVENTSTORE_OPTIMIZE_INDEX_MERGE` | 

**Default**: `false`

`OptimizeIndexMerge` allows faster merging of indexes when EventStoreDB has scavenged a chunk. This option has no effect on unscavenged chunks. When EventStoreDB has scavenged a chunk, and this option is set to `true`, it uses a bloom filter before reading the chunk to see if the value exists before reading the chunk to make sure that it still exists.

## Stream Existence Filter Size

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--stream-existence-filter-size` |
| YAML                 | `StreamExistenceFilterSize` |
| Environment variable | `EVENTSTORE_STREAM_EXISTENCE_FILTER_SIZE` | 

**Default**: `256000000`

`StreamExistenceFilterSize` is the amount of memory & disk space, in bytes, to use for the stream existence filter. This should be set to roughly the maximum number of streams you expect to have in your database, i.e if you expect to have a max of 500 million streams, use a value of 500000000. The value you select should also fit entirely in memory to avoid any performance degradation. Use 0 to disable the filter.

Upgrading to a version of EventStoreDB that supports the Stream Existence Filter requires the filter to be built - unless it is disabled. This will take approximately as long as it takes to read through the whole index.

Resizing the filter will also cause a full rebuild of the filter.

## Index Cache Size

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--index-cache-size` |
| YAML                 | `IndexCacheSize` |
| Environment variable | `EVENTSTORE_INDEX_CACHE_SIZE` | 

**Default**: `0`

`IndexCacheSize` is the maximum number of entries in each index LRU cache. The cache size is set to 0 (off) by default because it has an associated memory overhead and can be detrimental to workloads that produce a lot of cache misses. The cache is, however, well suited to read-heavy workloads of long lived streams.

The index LRU cache is only created for index files that have Bloom filters.

## Use Index Bloom Filters

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--use-index-bloom-filters` |
| YAML                 | `UseIndexBloomFilters` |
| Environment variable | `EVENTSTORE_USE_INDEX_BLOOM_FILTERS` | 

**Default**: `true`

`UseIndexBloomFilters` is a feature flag which can be used to disable the index Bloom filters. This should not be necessary and this flag will be removed in a future release, but is provided for safety since the index Bloom filters are a new feature. Please contact EventStore if you discover some need to disable this feature. 

Unless this flag is set to false, EventStoreDB creates a `.bloomfilter` file for each new PTable. The Bloom filter describes which streams are present in the PTable. This speeds up stream reads since EventStoreDB can avoid searching in index files do not contain the stream.

Note that immediately after upgrading to a version of EventStoreDB that produces index Bloom filters, no Bloom filters will yet exist. Either wait for new PTables to be produced with Bloom filters in the natural course of writing/merging/scavenging PTables, or rebuild the index for immediate generation.
