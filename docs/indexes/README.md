# Indexing

EventStoreDB stores indexes separately from the main data files, accessing records by stream name.

## Overview

EventStoreDB creates index entries as it processes commit events. It holds these in memory (called _memtables_) until it reaches the `MaxMemTableSize` and then persisted on disk in the _index_ folder along with an index map file.
The index files are uniquely named, and the index map file called _indexmap_. The index map describes the order and the level of the index file as well as containing the data checkpoint for the last written file, the version of the index map file and a checksum for the index map file. The logs refer to the index files as a _PTable_.

Indexes are sorted lists based on the hashes of stream names. To speed up seeking the correct location in the file of an entry for a stream, EventStoreDB keeps midpoints to relate the stream hash to the physical offset in the file.

As EventStoreDB saves more files, they are automatically merged together whenever there are more than 2 files at the same level into a single file at the next level. Each index entry is 24 bytes and the index file size is approximately 24Mb per 1M events.

Level 0 is the level of the _memtable_ that is kept in memory. Generally there is only 1 level 0 table unless an ongoing merge operation produces multiple level 0 tables.

Assuming the default `MaxMemTableSize` of 1M, the index files by level are:

| Level | Number of entries | Size          |
| ----- | ----------------- | ------------- |
| 1     | 1M                | 24MB          |
| 2     | 2M                | 48MB          |
| 3     | 4M                | 96MB          |
| 4     | 8M                | 192MB         |
| 5     | 16M               | 384MB         |
| 6     | 32M               | 768MB         |
| 7     | 64M               | 1536MB        |
| 8     | 128M              | 3072MB        |
| n     | 2^(n-1) * 1M      | 2^(n-1) * 24Mb|

Each index entry is 24 bytes and the index file size is approximately 24Mb per M events.

