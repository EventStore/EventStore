---
title: "How-to"
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

:::: code-group
::: code Linux
```bash
```bash:no-line-numbers
$ eventstored --version
EventStoreDB version 24.6.0.0 (oss-v24.6.0-alpha-16-g8e06f9f77/8e06f9f77, 2023-10-24T22:05:57-05:00)
```
:::
::: code Windows
```
> EventStore.ClusterNode.exe --version
EventStoreDB version 24.6.0.0 (oss-v24.6.0-alpha-16-g8e06f9f77/8e06f9f77, 2023-10-24T22:05:57-05:00)
```
:::
::::

The full list of available options is available from the currently installed server by using the `--help`
option in the command line.

### Configuration file

You would use the configuration file when you want the server to run with the same set of options every time.
YAML files are better for large installations as you can centrally distribute and manage them, or generate
them from a configuration management system.

The default configuration file name is `eventstore.conf` and it's located in
- **Linux:** `/etc/eventstore/`
- **Windows:** EventStoreDB installation directory

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
eventstored --log /tmp/eventstore/logs
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
$ eventstored --what-if
[68443, 1,17:38:39.178,INF] 
[68443, 1,17:38:39.186,INF] "OS ARCHITECTURE:"        X64 
[68443, 1,17:38:39.207,INF] "OS:"                     Linux ("Unix 5.19.0.1025")
[68443, 1,17:38:39.210,INF] "RUNTIME:"                ".NET 6.0.21/e40b3abf1" (64-bit)
[68443, 1,17:38:39.211,INF] "GC:"                     "3 GENERATIONS" "IsServerGC: False" "Latency Mode: Interactive"
[68443, 1,17:38:39.212,INF] "LOGS:"                   "/var/log/eventstore"
[68443, 1,17:38:39.251,INF] 
MODIFIED OPTIONS:
    Application Options:
         WHAT IF:                                 true (Command Line)


DEFAULT OPTIONS:
    Application Options:
         ALLOW ANONYMOUS ENDPOINT ACCESS:         True (<DEFAULT>)
         ALLOW ANONYMOUS STREAM ACCESS:           True (<DEFAULT>)
         ALLOW UNKNOWN OPTIONS:                   False (<DEFAULT>)
         CONFIG:                                  /etc/eventstore/eventstore.conf (<DEFAULT>)
         DISABLE HTTP CACHING:                    False (<DEFAULT>)
         ENABLE HISTOGRAMS:                       False (<DEFAULT>)
         HELP:                                    False (<DEFAULT>)
         LOG FAILED AUTHENTICATION ATTEMPTS:      False (<DEFAULT>)
         LOG HTTP REQUESTS:                       False (<DEFAULT>)
         MAX APPEND SIZE:                         1048576 (<DEFAULT>)
         SKIP INDEX SCAN ON READS:                False (<DEFAULT>)
         STATS PERIOD SEC:                        30 (<DEFAULT>)
         VERSION:                                 False (<DEFAULT>)
         WORKER THREADS:                          0 (<DEFAULT>)

    Authentication/Authorization Options:
         AUTHENTICATION CONFIG:                   <empty> (<DEFAULT>)
         AUTHENTICATION TYPE:                     internal (<DEFAULT>)
         AUTHORIZATION CONFIG:                    <empty> (<DEFAULT>)
         AUTHORIZATION TYPE:                      internal (<DEFAULT>)
         DISABLE FIRST LEVEL HTTP AUTHORIZATION:  False (<DEFAULT>)

    Certificate Options:
         CERTIFICATE RESERVED NODE COMMON NAME:   eventstoredb-node (<DEFAULT>)
         TRUSTED ROOT CERTIFICATES PATH:          /etc/ssl/certs (<DEFAULT>)

    Certificate Options (from file):
         CERTIFICATE FILE:                        <empty> (<DEFAULT>)
         CERTIFICATE PASSWORD:                    <empty> (<DEFAULT>)
         CERTIFICATE PRIVATE KEY FILE:            <empty> (<DEFAULT>)
         CERTIFICATE PRIVATE KEY PASSWORD:        <empty> (<DEFAULT>)

    Certificate Options (from store):
         CERTIFICATE STORE LOCATION:              <empty> (<DEFAULT>)
         CERTIFICATE STORE NAME:                  <empty> (<DEFAULT>)
         CERTIFICATE SUBJECT NAME:                <empty> (<DEFAULT>)
         CERTIFICATE THUMBPRINT:                  <empty> (<DEFAULT>)
         TRUSTED ROOT CERTIFICATE STORE LOCATION: <empty> (<DEFAULT>)
         TRUSTED ROOT CERTIFICATE STORE NAME:     <empty> (<DEFAULT>)
         TRUSTED ROOT CERTIFICATE SUBJECT NAME:   <empty> (<DEFAULT>)
         TRUSTED ROOT CERTIFICATE THUMBPRINT:     <empty> (<DEFAULT>)

    Cluster Options:
         CLUSTER DNS:                             fake.dns (<DEFAULT>)
         CLUSTER GOSSIP PORT:                     2113 (<DEFAULT>)
         CLUSTER SIZE:                            1 (<DEFAULT>)
         DEAD MEMBER REMOVAL PERIOD SEC:          1800 (<DEFAULT>)
         DISCOVER VIA DNS:                        True (<DEFAULT>)
         GOSSIP ALLOWED DIFFERENCE MS:            60000 (<DEFAULT>)
         GOSSIP INTERVAL MS:                      2000 (<DEFAULT>)
         GOSSIP SEED:                             <empty> (<DEFAULT>)
         GOSSIP TIMEOUT MS:                       2500 (<DEFAULT>)
         LEADER ELECTION TIMEOUT MS:              1000 (<DEFAULT>)
         NODE PRIORITY:                           0 (<DEFAULT>)
         QUORUM SIZE:                             1 (<DEFAULT>)
         READ ONLY REPLICA:                       False (<DEFAULT>)
         STREAM INFO CACHE CAPACITY:              0 (<DEFAULT>)
         UNSAFE ALLOW SURPLUS NODES:              False (<DEFAULT>)

    Database Options:
         ALWAYS KEEP SCAVENGED:                   False (<DEFAULT>)
         CACHED CHUNKS:                           -1 (<DEFAULT>)
         CHUNK INITIAL READER COUNT:              5 (<DEFAULT>)
         CHUNK SIZE:                              268435456 (<DEFAULT>)
         CHUNKS CACHE SIZE:                       536871424 (<DEFAULT>)
         COMMIT TIMEOUT MS:                       2000 (<DEFAULT>)
         DB:                                      /var/lib/eventstore (<DEFAULT>)
         DB LOG FORMAT:                           V2 (<DEFAULT>)
         DISABLE SCAVENGE MERGING:                False (<DEFAULT>)
         HASH COLLISION READ LIMIT:               100 (<DEFAULT>)
         INDEX:                                   <empty> (<DEFAULT>)
         INDEX CACHE DEPTH:                       16 (<DEFAULT>)
         INDEX CACHE SIZE:                        0 (<DEFAULT>)
         INITIALIZATION THREADS:                  1 (<DEFAULT>)
         MAX AUTO MERGE INDEX LEVEL:              2147483647 (<DEFAULT>)
         MAX MEM TABLE SIZE:                      1000000 (<DEFAULT>)
         MAX TRUNCATION:                          268435456 (<DEFAULT>)
         MEM DB:                                  False (<DEFAULT>)
         MIN FLUSH DELAY MS:                      2 (<DEFAULT>)
         OPTIMIZE INDEX MERGE:                    False (<DEFAULT>)
         PREPARE TIMEOUT MS:                      2000 (<DEFAULT>)
         READER THREADS COUNT:                    0 (<DEFAULT>)
         REDUCE FILE CACHE PRESSURE:              False (<DEFAULT>)
         SCAVENGE BACKEND CACHE SIZE:             67108864 (<DEFAULT>)
         SCAVENGE BACKEND PAGE SIZE:              16384 (<DEFAULT>)
         SCAVENGE HASH USERS CACHE CAPACITY:      100000 (<DEFAULT>)
         SCAVENGE HISTORY MAX AGE:                30 (<DEFAULT>)
         SKIP DB VERIFY:                          False (<DEFAULT>)
         SKIP INDEX VERIFY:                       False (<DEFAULT>)
         STATS STORAGE:                           File (<DEFAULT>)
         STREAM EXISTENCE FILTER SIZE:            256000000 (<DEFAULT>)
         UNBUFFERED:                              False (<DEFAULT>)
         UNSAFE DISABLE FLUSH TO DISK:            False (<DEFAULT>)
         UNSAFE IGNORE HARD DELETE:               False (<DEFAULT>)
         USE INDEX BLOOM FILTERS:                 True (<DEFAULT>)
         WRITE STATS TO DB:                       False (<DEFAULT>)
         WRITE THROUGH:                           False (<DEFAULT>)
         WRITE TIMEOUT MS:                        2000 (<DEFAULT>)

    Default User Options:
         DEFAULT ADMIN PASSWORD:                  **** (<DEFAULT>)
         DEFAULT OPS PASSWORD:                    **** (<DEFAULT>)

    Dev mode Options:
         DEV:                                     False (<DEFAULT>)
         REMOVE DEV CERTS:                        False (<DEFAULT>)

    gRPC Options:
         KEEP ALIVE INTERVAL:                     10000 (<DEFAULT>)
         KEEP ALIVE TIMEOUT:                      10000 (<DEFAULT>)

    Interface Options:
         ADVERTISE HOST TO CLIENT AS:             <empty> (<DEFAULT>)
         ADVERTISE HTTP PORT TO CLIENT AS:        0 (<DEFAULT>)
         ADVERTISE NODE PORT TO CLIENT AS:        0 (<DEFAULT>)
         CONNECTION PENDING SEND BYTES THRESHOLD: 10485760 (<DEFAULT>)
         CONNECTION QUEUE SIZE THRESHOLD:         50000 (<DEFAULT>)
         DISABLE ADMIN UI:                        False (<DEFAULT>)
         DISABLE GOSSIP ON HTTP:                  False (<DEFAULT>)
         DISABLE INTERNAL TCP TLS:                False (<DEFAULT>)
         DISABLE STATS ON HTTP:                   False (<DEFAULT>)
         ENABLE ATOM PUB OVER HTTP:               False (<DEFAULT>)
         ENABLE TRUSTED AUTH:                     False (<DEFAULT>)
         ENABLE UNIX SOCKET:                      False (<DEFAULT>)
         EXT HOST ADVERTISE AS:                   <empty> (<DEFAULT>)
         EXT IP:                                  127.0.0.1 (<DEFAULT>)
         GOSSIP ON SINGLE NODE:                   <empty> (<DEFAULT>)
         HTTP PORT:                               2113 (<DEFAULT>)
         HTTP PORT ADVERTISE AS:                  0 (<DEFAULT>)
         INT HOST ADVERTISE AS:                   <empty> (<DEFAULT>)
         INT IP:                                  127.0.0.1 (<DEFAULT>)
         INT TCP HEARTBEAT INTERVAL:              700 (<DEFAULT>)
         INT TCP HEARTBEAT TIMEOUT:               700 (<DEFAULT>)
         INT TCP PORT:                            1112 (<DEFAULT>)
         INT TCP PORT ADVERTISE AS:               0 (<DEFAULT>)
         NODE HEARTBEAT INTERVAL:                 2000 (<DEFAULT>)
         NODE HEARTBEAT TIMEOUT:                  1000 (<DEFAULT>)
         NODE HOST ADVERTISE AS:                  <empty> (<DEFAULT>)
         NODE IP:                                 127.0.0.1 (<DEFAULT>)
         NODE PORT:                               2113 (<DEFAULT>)
         NODE PORT ADVERTISE AS:                  0 (<DEFAULT>)
         REPLICATION HEARTBEAT INTERVAL:          700 (<DEFAULT>)
         REPLICATION HEARTBEAT TIMEOUT:           700 (<DEFAULT>)
         REPLICATION HOST ADVERTISE AS:           <empty> (<DEFAULT>)
         REPLICATION IP:                          127.0.0.1 (<DEFAULT>)
         REPLICATION PORT:                        1112 (<DEFAULT>)
         REPLICATION TCP PORT ADVERTISE AS:       0 (<DEFAULT>)

    Logging Options:
         DISABLE LOG FILE:                        False (<DEFAULT>)
         LOG:                                     /var/log/eventstore (<DEFAULT>)
         LOG CONFIG:                              logconfig.json (<DEFAULT>)
         LOG CONSOLE FORMAT:                      Plain (<DEFAULT>)
         LOG FILE INTERVAL:                       Day (<DEFAULT>)
         LOG FILE RETENTION COUNT:                31 (<DEFAULT>)
         LOG FILE SIZE:                           1073741824 (<DEFAULT>)
         LOG LEVEL:                               Default (<DEFAULT>)

    Projection Options:
         FAULT OUT OF ORDER PROJECTIONS:          False (<DEFAULT>)
         PROJECTION COMPILATION TIMEOUT:          500 (<DEFAULT>)
         PROJECTION EXECUTION TIMEOUT:            250 (<DEFAULT>)
         PROJECTION THREADS:                      3 (<DEFAULT>)
         PROJECTIONS QUERY EXPIRY:                5 (<DEFAULT>)
         RUN PROJECTIONS:                         None (<DEFAULT>)
         START STANDARD PROJECTIONS:              False (<DEFAULT>)

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

Autoconfiguration does not apply in containerized environments.

### StreamInfoCacheCapacity

This option sets the maximum number of entries to keep in the stream info cache. This is the lookup that
contains the information of any stream that has recently been read or written to. Having entries in this cache
significantly improves write and read performance to cached streams on larger databases.

By default, the cache dynamically resizes according to the amount of free memory. The minimum that it can be set to is 100,000 entries.

| Format               | Syntax                                  |
|:---------------------|:----------------------------------------|
| Command line         | `--stream-info-cache-capacity`          |
| YAML                 | `StreamInfoCacheCapacity`               |
| Environment variable | `EVENTSTORE_STREAM_INFO_CACHE_CAPACITY` |

The option is set to 0 by default, which enables dynamic resizing. The default on previous versions of
EventStoreDb was 100,000 entries.

::: note
The default value of 0 for `StreamInfoCacheCapacity` might not always be the best value for optimal performance. Ideally, it should be set to double the number of streams in the anticipated working set.

The total number of streams can be obtained by checking the event count in the `$streams` system stream. This stream is created by the [$streams system projection](projections.md#streams-projection).

It should be noted that the total number of streams does not necessarily give you the anticipated working set. The working set of streams is the set of streams that you intend on actively reading, writing, and/or subscribing to. This can be much lower than the total number of streams in certain cases, especially in systems that have many short-lived streams.
:::

### ReaderThreadsCount

This option configures the number of reader threads available to EventStoreDb. Having more reader threads
allows more concurrent reads to be processed.

The reader threads count will be set at startup to twice the number of available processors, with a minimum of
4 and a maximum of 16 threads.

| Format               | Syntax                            |
|:---------------------|:----------------------------------|
| Command line         | `--reader-threads-count`          |
| YAML                 | `ReaderThreadsCount`              |
| Environment variable | `EVENTSTORE_READER_THREADS_COUNT` |

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

| Format               | Syntax                      |
|:---------------------|:----------------------------|
| Command line         | `--worker-threads`          |
| YAML                 | `WorkerThreads`             |
| Environment variable | `EVENTSTORE_WORKER_THREADS` |

The option is set to 0 by default, which enables autoconfiguration. The default on previous versions of
EventStoreDb was 5 threads.

## Plugins configuration

The commercial edition of EventStoreDB ships with several plugins that augment the behavior of the open source server. Each plugin is documented in relevant sections. For example, under Security, you will find the User Certificates plugin documentation.

The plugins (apart from the `ldap` plugin) are configured separately to the main server configuration and can be configured via `json` files and `environment variables`.

Environment variables take precedence.

#### JSON files

Each configurable plugin comes with a sample JSON file in the `<installation-directory>/plugins/<plugin-name>` directory.
Here is an example file called `user-certificates-plugin-example.json` to enable the user certificates plugin.

```json
{
  "EventStore": {
    "Plugins": {
      "UserCertificates": {
        "Enabled": true
      }
    }
  }
}
```

The system looks for JSON configuration files in a `<installation-directory>/config/` directory, and on Linux and OS X, the server additionally looks in `/etc/eventstore/config/`. To take effect, JSON configuration must be saved to one of these locations.

The JSON configuration may be split across multiple files or combined into a single file. Apart from the `.json` extension, the names of the files is not important.

#### Environment variables

Any configuration can alternatively be provided via environment variables.

Use `__` as the level delimiter.

For example, the key configured above in json can also be set with the following environment variable:

```:no-line-numbers
EventStore__Plugins__UserCertificates__Enabled
```
