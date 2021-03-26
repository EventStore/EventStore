# Database backup

Backing up an EventStoreDB database is straightforward, but relies on you carrying out the steps below in the correct order.

## Backing up a database

1.  Copy all _*.chk_ files to your backup location.
2.  Copy the remaining files and directories to your backup location.

For example:

```bash
rsync -a /data/eventstore/db/*.chk /backup/eventstore/db/
rsync -a /data/eventstore/db/index /backup/eventstore/db/
rsync -a /data/eventstore/db/*.0* /backup/eventstore/db/
```

## Restoring a database

1.  Ensure your EventStoreDB database is stopped. Restoring a database on running instance is not possible and in most cases will lead to data corruption.
2.  Copy all files to the desired location.
3.  Create a copy of _chaser.chk_ and call it _truncate.chk_ (which override already existing _truncate.chk_).

## Differential backup

EventStoreDB keeps most of the data in _chunk files_, named `chunkX.Y`, where `X` is the chunk number, and `Y` is the version of that chunk file. As EventStoreDB scavenges, it creates new versions of scavenged chunks which are interchangeable with older versions (but for the removed data).

It's only necessary to keep the file whose name has the highest `Y` for each `X`, as well as the checkpoint files and the index directory (to avoid expensive index rebuilding).

## Other options

There are many other options available for backing up an EventStoreDB database. You can find the most common options here.

### Additional node

Increase the cluster node counts to keep further copies of data. Increasing the number of cluster nodes, however, impacts the write performance of the cluster as more nodes need to confirm each write. 

Alternatively, you can use a [read-only replica](../clustering/node-roles.md#read-only-replica) node, which is not a part of the cluster. In this case, the write performance won't be affected.

### Alternative storage

Set up a durable subscription that writes all events to another storage mechanism such as a key/value or column store. These methods would require a manual set up for restoring back to a cluster group.

### Backup cluster

Use a second EventStoreDB cluster as a back up. Such a strategy is known as a primary/secondary back up scheme. 

The primary cluster asynchronously pushes data to the second cluster using a durable subscription. The second cluster is available in case of a disaster on the primary cluster. If you are using this strategy, we recommend you only to support manual failover from primary to secondary as automated strategies risk causing a [split brain](http://en.wikipedia.org/wiki/Split-brain_%28computing%29) problem.
