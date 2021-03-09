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

::: tip
Many people do not rely on hot backups in a highly available cluster but instead increase their node counts to keep further copies of data.
:::

## Differential backup

EventStoreDB keep most data in _chunk files_, named `chunkX.Y`, where X is the chunk number, and Y is the version of that chunk file. As EventStoreDB scavenges, it creates new versions of scavenged chunks which are interchangeable with older versions (but for the removed data).

It's only necessary to keep the file whose name has the highest `Y` for each `X`, as well as the checkpoint files and the index directory (to avoid expensive index rebuilding).

## Other options

There are many other options available for backing up an EventStoreDB database. For example, you can set up a durable subscription that writes all events to another storage mechanism such as a key/value or column store. These methods would require a manual set up for restoring back to a cluster group.

You can expand upon this option to use a second EventStoreDB node/cluster as a back up. This is commonly known as a primary/secondary back up scheme. The primary cluster runs and asynchronously pushes data to a second cluster as described above. The second cluster/node is available in case of disaster on the primary cluster. If you are using this strategy then we recommend you only support manual failover from Primary to Secondary as automated strategies risk causing a [split brain](http://en.wikipedia.org/wiki/Split-brain_%28computing%29) problem.
