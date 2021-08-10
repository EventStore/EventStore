# Database backup and restore

Backing up an EventStoreDB database is straightforward but relies on carrying out the steps below in the correct order.

## Types of backups

There are two main ways to perform backups:

### Disk Snapshotting 

If your infrastructure is virtualized, disk _snapshots_ is an option and the easiest to perform backup and restore operations.

### Regular file copy

- Simple full backup: when the DB size is small and the frequency of appends is low.
- Differential backup: when the DB size is large or the system has a high append frequency.

## Considerations for backup and restore procedures

Backing up one node is recommended.
However, ensure that the node chosen as a target for the backup is:
- up to date
- connected to the cluster.

For additional safety, you can also backup at least a quorum of nodes.

Do not back up a node at the same time as running a scavenge operation.

[Read-only replica](../clustering/node-roles.md#read-only-replica) nodes may be used as backup source.

When either running a backup or restoring, do not mix backup files of different nodes.

The restore must happen on a _stopped_ node.

The restore process can happen on any node of a cluster.

You can restore any number of nodes in a cluster from the same backup source. It means, for example, in the event non-recoverable three nodes cluster, that the same backup source can be used to restore a completely new three nodes cluster.

When you restore a node that was the backup source, perform a full backup after recovery.  

## Database Files Information

By default, there are two directories containing data that needs to be included in the backup:
- `db\ ` where the data is located
- `index\ ` where the indexes are kept.

The exact name and location are dependent on your configuration.
- `db\ ` contains:
    - the chunks files, named `chk-X.Y` where `X` is the chunk number and `Y` the version.
    - the checkpoints files, `*.chk` (`chaser.chk`, `epoch.chk`, `proposal.chk`, `truncate.chk`, `writer.chk`)
- `index\ ` contains: 
    - the index map: `indexmap`
    - the indexes: UUID named files , e.g `5a1a8395-94ee-40c1-bf93-aa757b5887f8`

## Disks Snapshot

If the `db\ ` and `index\ ` directories are on the same volume, a snapshot of that volume is enough.

However, if they are on different volumes, take first a snapshot 
of the volume containing the `index\ ` directory and then a snapshot of the volume containing the `db\ ` directory.
    
## Simple full backup & restore

### Backup 

1. Copy any index checkpoint files (`<index directory>\**\*.chk`) to your backup location.
2. Copy the other index files to your backup location (the rest of `<index directory>`, excluding the checkpoints).
3. Copy the database checkpoint files (`*.chk`) to your backup location.
4. Copy the chunk files (`chunk-X.Y`) to your backup location.

For example, with a database in `data` and index in `data/index`:

``` bash
rsync -aIR data/./index/**/*.chk backup
rsync -aI --exclude '*.chk' data/index backup
rsync -aI data/*.chk backup
rsync -a data/*.0* backup
```

### Restoring a database

1. Ensure the EventStoreDB process is stopped. Restoring a database on running instance is not possible and, in most cases, will lead to data corruption.
2. Copy all files to the desired location.
3. Create a copy of `chaser.chk` and call it `truncate.chk`. This effectively overwrites the restored `truncate.chk`.

## Differential backup & restore

The following procedure is designed to minimize the backup storage space, and can be used to do a full and differential backup.

### Backing up

1. Make a list of the difference between files in the backup location and the source location.
2. List for removal of any chunks (`chunk-X.Y`) or index files in the backup that are not also in the source (These will be scavenged chunks or merged indexes.)
3. Copy the `indexmap` file to backup.
4. Copy all non-writer checkpoints (`chaser.chk`, `epoch.chk`, `proposal.chk`, `truncate.chk`).
5. Copy the writer checkpoint (`writer.chk`).
6. List for copy all chunks (`chunk-X.Y`) & index files in source.
7. Copy all marked for copy files in source, skipping matching file names in the backup.
8. Delete all files marked for removal in the backup directory.

If any files listed for removal (step 2) or copy (step 6) are changed during the backup, the backup may be stopped and restarted. In this case, only newly changed files will be saved.

### Restoring a database

1. Ensure the  Event Store DB process is stopped. Restoring a database on running instance is not possible and, in most cases, will lead to data corruption.
2. Copy all files to the desired location.
3. Create a copy of `chaser.chk` and call it `truncate.chk`. It effectively overwrites the restored `truncate.chk`.


## Other options

There are other options available for ensuring data recovery, that are not strictly related to backups.

### Additional node (aka Hot Backup)

Increase the cluster size from 3 to 5 to keep further copies of data. This increase in the cluster size will slow the cluster's writing performance as two follower nodes will need to confirm each write. 

Alternatively, you can use a [read-only replica](../clustering/node-roles.md#read-only-replica) node, which is not a part of the cluster.
In this case, the write performance will be minimally impacted.

### Alternative storage

Set up a durable subscription that writes all events to another storage mechanism such as a key/value or column store. These methods would require a manual set up for restoring a cluster node or group.

### Backup cluster

Use a second EventStoreDB cluster as a backup. Such a strategy is known as a primary/secondary back up scheme. 

The primary cluster asynchronously pushes data to the second cluster using a durable subscription. The second cluster is available in case of a disaster on the primary cluster.

If you are using this strategy, we recommend you only support manual failover from primary to secondary as automated strategies risk causing a [split brain](http://en.wikipedia.org/wiki/Split-brain_%28computing%29) problem.
