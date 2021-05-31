# Auto-Configured Options

Some options are configured at startup to make better use of the available resources on larger instances or machines.

These options are `StreamInfoCacheCapacity`, `ReaderThreadsCount`, and `WorkerThreads`.

## StreamInfoCacheCapacity

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--stream-info-cache-capacity` |
| YAML                 | `StreamInfoCacheCapacity` |
| Environment variable | `STREAM_INFO_CACHE_CAPACITY` | 

This option sets the maximum number of entries to keep in the stream info cache. This is the lookup that contains the information of any stream that has recently been read or written to. Having entries in this cache significantly improves write and read performance to cached streams on larger databases.

The cache is configured at startup based on the available free memory at the time. If there is 4gb or more available, it will be configured to take at most 75% of the remaining memory, otherwise it will take at most 50%. The minimum that it can be set to is 100,000 entries.

The option is set to 0 by default, which enables auto-configuration. The default on previous versions of EventStoreDb was 100,000 entries.

## ReaderThreadsCount

| Option Name               | Syntax |
| :------------------- | :----- |
| Command line         | `--reader-threads-count` |
| YAML                 | `ReaderThreadsCount` |
| Environment variable | `READER_THREADS_COUNT` | 

This option configures the number of reader threads available to EventStoreDb. Having more reader threads allows more concurrent reads to be processed.

The reader threads count will be set at startup to twice the number of available processors, with a minimum of 4 and a maximum of 16 threads.

The option is set to 0 by default, which enables auto-configuration. The default on previous versions of EventStoreDb was 4 threads.

:::warning
Increasing the reader threads count too high can cause read timeouts if your disk cannot handle the increased load.
:::

## WorkerThreads

| Option Name               | Syntax |
| :------------------- | :----- |
| Command line         | `--worker-threads` |
| YAML                 | `WorkerThreads` |
| Environment variable | `WORKER_THREADS` | 

Worker Threads configures the number of threads available to the pool of worker services.

At startup the number of worker threads will be set to 10 if there are more than 4 reader threads. Otherwise, it will be set to have 5 threads available.

The option is set to 0 by default, which enables auto-configuration. The default on previous versions of EventStoreDb was 5 threads.
