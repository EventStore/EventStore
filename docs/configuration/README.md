# Options

EventStoreDB has a number of configuration options that can be changed. You can find all the options described in details in this section.

When you don't change the configuration, EventStoreDB will use sensible defaults, but they might not suit your needs. You can always instruct EventStoreDB to use a different set of options. There are multiple ways to configure EventStoreDB server, described below.

## Version and help

You can check what version of EventStoreDB you have installed by using the `--version` parameter in the command line. For example:

```bash
$ eventstore --version
EventStoreDB version v21.6.0 (tags/oss-v21.6.0/1f713a407, Unknown)
```

The full list of available options is available from the currently installed server by using the `--help` option in the command line.

## Configuration file

You would use the configuration file when you want the server to run with the same set of options every time. YAML files are better for large installations as you can centrally distribute and manage them, or generate them from a configuration management system.

The configuration file has YAML-compatible format. The basic format of the YAML configuration file is as follows:

```yaml
---
Db: "/volumes/data"
Log: "/esdb/logs"
```

:::tip
You need to use the three dashes and spacing in your YAML file.
:::

The default configuration file name is `eventstore.conf`. It is located in `/etc/eventstore/` on Linux and the server installation directory on Windows. You can either change this file or create another file and instruct EventStoreDB to use it.

To tell the EventStoreDB server to use a different configuration file, you pass the file path on the command line with `--config=filename`, or use the `CONFIG` environment variable.

## Environment variables

You can also set all arguments with environment variables. All variables are prefixed with `EVENTSTORE_` and normally follow the pattern `EVENTSTORE_{option}`. For example, setting the `EVENTSTORE_LOG` variable would instruct the server to use a custom location for log files.

Environment variables override all the options specified in configuration files.

## Command line

You can also override options from both configuration files and environment variables using the command line.

For example, starting EventStoreDB with the `--log` option will override the default log files location:

:::: code-group
::: code Linux
```bash
eventstore --log /tmp/eventstore/logs
```
:::
::: code Windows
```
EventStore.ClusterNode.exe --log C:\Temp\EventStore\Logs
```
:::
::::

## Testing the configuration

If more than one method is used to configure the server, it might be hard to find out what the effective configuration will be when the server starts. To help finding out just that, you can use the `--what-if` option. 

When you run EventStoreDB with this option, it will print out the effective configuration applied from all available sources (default and custom configuration file, environment variables and command line parameters) and print it out to the console.

::: detail Click here to see a WhatIf example
```
$ eventstore --what-if
[1085525, 1,09:19:40.884,INF]
"ES VERSION:"             "21.6.0.0" ("tags/oss-v21.6.0"/"1f713a407", "Thu, 24 Jun 2021 11:49:23 +0400")
[1085525, 1,09:19:40.902,INF] "OS:"                     Linux ("Unix 5.4.0.66")
[1085525, 1,09:19:40.905,INF] "RUNTIME:"                ".NET 5.0.3" (64-bit)
[1085525, 1,09:19:40.905,INF] "GC:"                     "3 GENERATIONS"
[1085525, 1,09:19:40.905,INF] "LOGS:"                   "/var/log/eventstore"
[1085525, 1,09:19:40.936,INF] MODIFIED OPTIONS:

     WHAT IF:                                 true (Command Line)

DEFAULT OPTIONS:

     HELP:                                    False (<DEFAULT>)
     VERSION:                                 False (<DEFAULT>)
     LOG:                                     /var/log/eventstore (<DEFAULT>)
     LOG LEVEL:                               Default (<DEFAULT>)
     CONFIG:                                  /etc/eventstore/eventstore.conf (<DEFAULT>)
     START STANDARD PROJECTIONS:              False (<DEFAULT>)
     DISABLE HTTP CACHING:                    False (<DEFAULT>)
     STATS PERIOD SEC:                        30 (<DEFAULT>)
     WORKER THREADS:                          5 (<DEFAULT>)
     ENABLE HISTOGRAMS:                       False (<DEFAULT>)
     LOG HTTP REQUESTS:                       False (<DEFAULT>)
     LOG FAILED AUTHENTICATION ATTEMPTS:      False (<DEFAULT>)
     SKIP INDEX SCAN ON READS:                False (<DEFAULT>)
     MAX APPEND SIZE:                         1048576 (<DEFAULT>)
     INSECURE:                                False (<DEFAULT>)
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
     STREAM INFO CACHE CAPACITY:              100000 (<DEFAULT>)
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
     READER THREADS COUNT:                    4 (<DEFAULT>)
     MAX AUTO MERGE INDEX LEVEL:              2147483647 (<DEFAULT>)
     WRITE STATS TO DB:                       False (<DEFAULT>)
     MAX TRUNCATION:                          268435456 (<DEFAULT>)
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
```
:::

::: note
Version 21.6 introduced a stricter configuration check: the server will _not start_ when an unknown configuration options is passed in either the configuration file, environment variable or command line.

E.g: the following will prevent the server from starting:
* `--UnknownConfig` on the command line
* `EVENTSTORE_UnknownConfig` through environment variable 
* `UnknownConfig: value` in the config file

And will output on `stdout` only: `Error while parsing options: The option UnknownConfig is not a known option. (Parameter 'UnknownConfig')`.
:::