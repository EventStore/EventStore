---
title: "Configuration"
---

## Configuration options

EventStoreDB has a number of configuration options that can be changed. You can find all the options described
in details in this section.

When you don't change the configuration, EventStoreDB will use sensible defaults, but they might not suit your
needs. You can always instruct EventStoreDB to use a different set of options. There are multiple ways to
configure EventStoreDB server, described below.

### Version and help

You can check what version of EventStoreDB you have installed by using the `--version` parameter in the
command line. For example:

```bash:no-line-numbers
$ eventstore --version
EventStoreDB version 21.10.0.0 (tags/oss-v21.10.0/076363440, Mon, 1 Nov 2021 17:10:49 +0000)
```

The full list of available options is available from the currently installed server by using the `--help`
option in the command line.

### Configuration file

You would use the configuration file when you want the server to run with the same set of options every time.
YAML files are better for large installations as you can centrally distribute and manage them, or generate
them from a configuration management system.

The configuration file has YAML-compatible format. The basic format of the YAML configuration file is as
follows:

```yaml:no-line-numbers
---
Db: "/volumes/data"
Log: "/esdb/logs"
```

::: tip 
You need to use the three dashes and spacing in your YAML file.
:::

The default configuration file name is `eventstore.conf`. It is located in `/etc/eventstore/` on Linux and the
server installation directory on Windows. You can either change this file or create another file and instruct
EventStoreDB to use it.

To tell the EventStoreDB server to use a different configuration file, you pass the file path on the command
line with `--config=filename`, or use the `CONFIG`
environment variable.

### Environment variables

You can also set all arguments with environment variables. All variables are prefixed with `EVENTSTORE_` and
normally follow the pattern `EVENTSTORE_{option}`. For example, setting the `EVENTSTORE_LOG`
variable would instruct the server to use a custom location for log files.

Environment variables override all the options specified in configuration files.

### Command line

You can also override options from both configuration files and environment variables using the command line.

For example, starting EventStoreDB with the `--log` option will override the default log files location:

:::: code-group
::: code-group-item Linux
```bash:no-line-numbers
eventstore --log /tmp/eventstore/logs
```
:::
::: code-group-item Windows
```bash:no-line-numbers
EventStore.ClusterNode.exe --log C:\Temp\EventStore\Logs
```
:::
::::

### Testing the configuration

If more than one method is used to configure the server, it might be hard to find out what the effective
configuration will be when the server starts. To help to find out just that, you can use the `--what-if`
option.

When you run EventStoreDB with this option, it will print out the effective configuration applied from all
available sources (default and custom configuration file, environment variables and command line parameters)
and print it out to the console.

::: details Click here to see a WhatIf example

```
$ eventstore --what-if
[16540, 1,13:03:50.994,INF]
"ES VERSION:"             "21.10.0.0" ("tags/oss-v21.10.0"/"076363440", "Unknown")
[16540, 1,13:03:51.011,INF] "OS:"                     Linux ("Unix 5.4.0.66")
[16540, 1,13:03:51.012,INF] "RUNTIME:"                ".NET 5.0.11/f431858f8" (64-bit)
[16540, 1,13:03:51.012,INF] "GC:"                     "3 GENERATIONS"
[16540, 1,13:03:51.012,INF] "LOGS:"                   "/var/log/eventstore"
[16540, 1,13:03:51.035,INF] MODIFIED OPTIONS:

     WHAT IF:                                 true (Command Line)

DEFAULT OPTIONS:

     HELP:                                    False (<DEFAULT>)
     VERSION:                                 False (<DEFAULT>)
     CONFIG:                                  /etc/eventstore/eventstore.conf (<DEFAULT>)
     START STANDARD PROJECTIONS:              False (<DEFAULT>)
     DISABLE HTTP CACHING:                    False (<DEFAULT>)
     STATS PERIOD SEC:                        30 (<DEFAULT>)
     WORKER THREADS:                          0 (<DEFAULT>)
     ENABLE HISTOGRAMS:                       False (<DEFAULT>)
     LOG HTTP REQUESTS:                       False (<DEFAULT>)
     LOG FAILED AUTHENTICATION ATTEMPTS:      False (<DEFAULT>)
     SKIP INDEX SCAN ON READS:                False (<DEFAULT>)
     MAX APPEND SIZE:                         1048576 (<DEFAULT>)
     INSECURE:                                False (<DEFAULT>)
     LOG:                                     /var/log/eventstore (<DEFAULT>)
     LOG CONFIG:                              logconfig.json (<DEFAULT>)
     LOG LEVEL:                               Default (<DEFAULT>)
     LOG CONSOLE FORMAT:                      Plain (<DEFAULT>)
     LOG FILE SIZE:                           1073741824 (<DEFAULT>)
     LOG FILE INTERVAL:                       Day (<DEFAULT>)
     LOG FILE RETENTION COUNT:                31 (<DEFAULT>)
     DISABLE LOG FILE:                        False (<DEFAULT>)
     AUTHORIZATION TYPE:                      internal (<DEFAULT>)
     AUTHORIZATION CONFIG:                    <empty> (<DEFAULT>)
     AUTHENTICATION TYPE:                     internal (<DEFAULT>)
     AUTHENTICATION CONFIG:                   <empty> (<DEFAULT>)
     DISABLE FIRST LEVEL HTTP AUTHORIZATION:  False (<DEFAULT>)
     TRUSTED ROOT CERTIFICATES PATH:          <empty> (<DEFAULT>)
     CERTIFICATE RESERVED NODE COMMON NAME:   eventstoredb-node (<DEFAULT>)
     CERTIFICATE FILE:                        <empty> (<DEFAULT>)
     CERTIFICATE PRIVATE KEY FILE:            <empty> (<DEFAULT>)
     CERTIFICATE PASSWORD:                    <empty> (<DEFAULT>)
     CERTIFICATE STORE LOCATION:              <empty> (<DEFAULT>)
     CERTIFICATE STORE NAME:                  <empty> (<DEFAULT>)
     CERTIFICATE SUBJECT NAME:                <empty> (<DEFAULT>)
     CERTIFICATE THUMBPRINT:                  <empty> (<DEFAULT>)
     STREAM INFO CACHE CAPACITY:              0 (<DEFAULT>)
     CLUSTER SIZE:                            1 (<DEFAULT>)
     NODE PRIORITY:                           0 (<DEFAULT>)
     COMMIT COUNT:                            -1 (<DEFAULT>)
     PREPARE COUNT:                           -1 (<DEFAULT>)
     DISCOVER VIA DNS:                        True (<DEFAULT>)
     CLUSTER DNS:                             fake.dns (<DEFAULT>)
     CLUSTER GOSSIP PORT:                     2113 (<DEFAULT>)
     GOSSIP SEED:                             <empty> (<DEFAULT>)
     GOSSIP INTERVAL MS:                      2000 (<DEFAULT>)
     GOSSIP ALLOWED DIFFERENCE MS:            60000 (<DEFAULT>)
     GOSSIP TIMEOUT MS:                       2500 (<DEFAULT>)
     READ ONLY REPLICA:                       False (<DEFAULT>)
     UNSAFE ALLOW SURPLUS NODES:              False (<DEFAULT>)
     DEAD MEMBER REMOVAL PERIOD SEC:          1800 (<DEFAULT>)
     LEADER ELECTION TIMEOUT MS:              1000 (<DEFAULT>)
     QUORUM SIZE:                             1 (<DEFAULT>)
     PREPARE ACK COUNT:                       1 (<DEFAULT>)
     COMMIT ACK COUNT:                        1 (<DEFAULT>)
     MIN FLUSH DELAY MS:                      2 (<DEFAULT>)
     DISABLE SCAVENGE MERGING:                False (<DEFAULT>)
     SCAVENGE HISTORY MAX AGE:                30 (<DEFAULT>)
     CACHED CHUNKS:                           -1 (<DEFAULT>)
     CHUNKS CACHE SIZE:                       536871424 (<DEFAULT>)
     MAX MEM TABLE SIZE:                      1000000 (<DEFAULT>)
     HASH COLLISION READ LIMIT:               100 (<DEFAULT>)
     DB:                                      /var/lib/eventstore (<DEFAULT>)
     INDEX:                                   <empty> (<DEFAULT>)
     MEM DB:                                  False (<DEFAULT>)
     USE INDEX BLOOM FILTERS:                 True (<DEFAULT>)
     INDEX CACHE SIZE:                        0 (<DEFAULT>)
     SKIP DB VERIFY:                          False (<DEFAULT>)
     WRITE THROUGH:                           False (<DEFAULT>)
     UNBUFFERED:                              False (<DEFAULT>)
     CHUNK INITIAL READER COUNT:              5 (<DEFAULT>)
     PREPARE TIMEOUT MS:                      2000 (<DEFAULT>)
     COMMIT TIMEOUT MS:                       2000 (<DEFAULT>)
     WRITE TIMEOUT MS:                        2000 (<DEFAULT>)
     UNSAFE DISABLE FLUSH TO DISK:            False (<DEFAULT>)
     UNSAFE IGNORE HARD DELETE:               False (<DEFAULT>)
     SKIP INDEX VERIFY:                       False (<DEFAULT>)
     INDEX CACHE DEPTH:                       16 (<DEFAULT>)
     OPTIMIZE INDEX MERGE:                    False (<DEFAULT>)
     ALWAYS KEEP SCAVENGED:                   False (<DEFAULT>)
     REDUCE FILE CACHE PRESSURE:              False (<DEFAULT>)
     INITIALIZATION THREADS:                  1 (<DEFAULT>)
     READER THREADS COUNT:                    0 (<DEFAULT>)
     MAX AUTO MERGE INDEX LEVEL:              2147483647 (<DEFAULT>)
     WRITE STATS TO DB:                       False (<DEFAULT>)
     MAX TRUNCATION:                          268435456 (<DEFAULT>)
     CHUNK SIZE:                              268435456 (<DEFAULT>)
     STATS STORAGE:                           File (<DEFAULT>)
     DB LOG FORMAT:                           V2 (<DEFAULT>)
     STREAM EXISTENCE FILTER SIZE:            256000000 (<DEFAULT>)
     KEEP ALIVE INTERVAL:                     10000 (<DEFAULT>)
     KEEP ALIVE TIMEOUT:                      10000 (<DEFAULT>)
     INT IP:                                  127.0.0.1 (<DEFAULT>)
     EXT IP:                                  127.0.0.1 (<DEFAULT>)
     HTTP PORT:                               2113 (<DEFAULT>)
     ENABLE EXTERNAL TCP:                     False (<DEFAULT>)
     INT TCP PORT:                            1112 (<DEFAULT>)
     EXT TCP PORT:                            1113 (<DEFAULT>)
     EXT HOST ADVERTISE AS:                   <empty> (<DEFAULT>)
     INT HOST ADVERTISE AS:                   <empty> (<DEFAULT>)
     ADVERTISE HOST TO CLIENT AS:             <empty> (<DEFAULT>)
     ADVERTISE HTTP PORT TO CLIENT AS:        0 (<DEFAULT>)
     ADVERTISE TCP PORT TO CLIENT AS:         0 (<DEFAULT>)
     EXT TCP PORT ADVERTISE AS:               0 (<DEFAULT>)
     HTTP PORT ADVERTISE AS:                  0 (<DEFAULT>)
     INT TCP PORT ADVERTISE AS:               0 (<DEFAULT>)
     INT TCP HEARTBEAT TIMEOUT:               700 (<DEFAULT>)
     EXT TCP HEARTBEAT TIMEOUT:               1000 (<DEFAULT>)
     INT TCP HEARTBEAT INTERVAL:              700 (<DEFAULT>)
     EXT TCP HEARTBEAT INTERVAL:              2000 (<DEFAULT>)
     GOSSIP ON SINGLE NODE:                   <empty> (<DEFAULT>)
     CONNECTION PENDING SEND BYTES THRESHOLD: 10485760 (<DEFAULT>)
     CONNECTION QUEUE SIZE THRESHOLD:         50000 (<DEFAULT>)
     DISABLE ADMIN UI:                        False (<DEFAULT>)
     DISABLE STATS ON HTTP:                   False (<DEFAULT>)
     DISABLE GOSSIP ON HTTP:                  False (<DEFAULT>)
     ENABLE TRUSTED AUTH:                     False (<DEFAULT>)
     DISABLE INTERNAL TCP TLS:                False (<DEFAULT>)
     DISABLE EXTERNAL TCP TLS:                False (<DEFAULT>)
     ENABLE ATOM PUB OVER HTTP:               False (<DEFAULT>)
     RUN PROJECTIONS:                         None (<DEFAULT>)
     PROJECTION THREADS:                      3 (<DEFAULT>)
     PROJECTIONS QUERY EXPIRY:                5 (<DEFAULT>)
     FAULT OUT OF ORDER PROJECTIONS:          False (<DEFAULT>)
     PROJECTION COMPILATION TIMEOUT:          500 (<DEFAULT>)
     PROJECTION EXECUTION TIMEOUT:            250 (<DEFAULT>)
```
:::

::: note 
Version 21.6 introduced a stricter configuration check: the server will _not start_ when an unknown
configuration options is passed in either the configuration file, environment variable or command line.

E.g: the following will prevent the server from starting:

* `--UnknownConfig` on the command line
* `EVENTSTORE_UnknownConfig` through environment variable
* `UnknownConfig: value` in the config file

And will output on `stdout`
only: _Error while parsing options: The option UnknownConfig is not a known option. (Parameter 'UnknownConfig')_.
:::

## Autoconfigured options

Some options are configured at startup to make better use of the available resources on larger instances or
machines.

These options are `StreamInfoCacheCapacity`, `ReaderThreadsCount`, and `WorkerThreads`.

### StreamInfoCacheCapacity

This option sets the maximum number of entries to keep in the stream info cache. This is the lookup that
contains the information of any stream that has recently been read or written to. Having entries in this cache
significantly improves write and read performance to cached streams on larger databases.

The cache is configured at startup based on the available free memory at the time. If there is 4gb or more
available, it will be configured to take at most 75% of the remaining memory, otherwise it will take at most
50%. The minimum that it can be set to is 100,000 entries.

| Format               | Syntax                         |
|:---------------------|:-------------------------------|
| Command line         | `--stream-info-cache-capacity` |
| YAML                 | `StreamInfoCacheCapacity`      |
| Environment variable | `STREAM_INFO_CACHE_CAPACITY`   | 

The option is set to 0 by default, which enables autoconfiguration. The default on previous versions of
EventStoreDb was 100,000 entries.

### ReaderThreadsCount

This option configures the number of reader threads available to EventStoreDb. Having more reader threads
allows more concurrent reads to be processed.

The reader threads count will be set at startup to twice the number of available processors, with a minimum of
4 and a maximum of 16 threads.

| Format               | Syntax                   |
|:---------------------|:-------------------------|
| Command line         | `--reader-threads-count` |
| YAML                 | `ReaderThreadsCount`     |
| Environment variable | `READER_THREADS_COUNT`   | 

The option is set to 0 by default, which enables autoconfiguration. The default on previous versions of
EventStoreDb was 4 threads.

::: warning 
Increasing the reader threads count too high can cause read timeouts if your disk cannot handle the
increased load.
:::

### WorkerThreads

The `WorkerThreads` option configures the number of threads available to the pool of worker services.

At startup the number of worker threads will be set to 10 if there are more than 4 reader threads. Otherwise,
it will be set to have 5 threads available.

| Format               | Syntax             |
|:---------------------|:-------------------|
| Command line         | `--worker-threads` |
| YAML                 | `WorkerThreads`    |
| Environment variable | `WORKER_THREADS`   | 

The option is set to 0 by default, which enables autoconfiguration. The default on previous versions of
EventStoreDb was 5 threads.
