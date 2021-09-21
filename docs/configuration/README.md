# Options

EventStoreDB has a number of configuration options that can be changed. You can find all the options described in details in this section.

When you don't change the configuration, EventStoreDB will use sensible defaults, but they might not suit your needs. You can always instruct EventStoreDB to use a different set of options. There are multiple ways to configure EventStoreDB server, described below.

## Version and help

You can check what version of EventStoreDB you have installed by using the `--version` parameter in the command line. For example:

:::: code-group
::: code Linux

```bash
$ eventstored --version
EventStoreDB version v20.1.0 (tags/oss-v20.6.1/9ea108855, Unknown)
```
:::
::: code Windows

```
> EventStore.ClusterNode.exe --version
EventStoreDB version v20.1.0 (tags/oss-v20.6.1/9ea108855, Unknown)
```
:::
::::

The full list of available options is available from the currently installed server by using the `--help` option in the command line.

## Configuration file

You would use the configuration file when you want the server to run with the same set of options every time. YAML files are better for large installations as you can centrally distribute and manage them, or generate them from a configuration management system.

The configuration file has YAML-compatible format. The basic format of the YAML configuration file is as follows:

```yaml
---
Db: "/volumes/data"
Log: "/esdb/logs"
```

::: tip
You need to use the three dashes and spacing in your YAML file.
:::

The default configuration file name is `eventstore.conf`. It is located in `/etc/eventstore/` on Linux and the server installation directory on Windows. You can either change this file or create another file and instruct EventStoreDB to use it.

To tell the EventStoreDB server to use a different configuration file, you pass the file path on the command line with `--config=filename`, or use the `CONFIG` environment variable.

## Environment variables

You can set all arguments can also as environment variables. All variables are prefixed with `EVENTSTORE_` and normally follow the pattern `EVENTSTORE_{option}`. For example, setting the `EVENTSTORE_LOG` variable would instruct the server to use a custom location for log files.

Environment variables override all the options specified in configuration files.

## Command line

You can also override options from both configuration files and environment variables using the command line.

For example, starting EventStoreDB with the `--log` option will override the default log files location:

:::: code-group
::: code Linux

```bash
eventstored --log /tmp/eventstore/logs
```
:::
::: code Windows

```
EventStore.ClusterNode.exe --log C:\Temp\EventStore\Logs
```
:::
::::

## Testing the configuration

If more than one method is used to configure the server, it might be hard to find out what the effective configuration will be when the server starts. You can use the `--what-if` option for that purpose. 

When you run EventStoreDB with this option, it will print out the effective configuration applied from all available sources (default and custom configuration file, environment variables and command line parameters) and print it out to the console.

::: warning
Always run EventStoreDB as a service first, so it creates all the necessary directories using the service permissions. Running the service executable with `--what-if` option before starting the service _will create the data and log directories_ owned by the current user. It might prevent the service from running properly due to lack of write permissions for those directories.
:::

::: details Click here to see a WhatIf example
```
$ eventstored --what-if
[    1, 1,21:25:31.253,INF] "ES VERSION:"             "20.6.1.0" ("tags/oss-v20.6.1"/"9ea108855", "Unknown")
[    1, 1,21:25:31.270,INF] "OS:"                     Linux ("Unix 4.19.76.0")
[    1, 1,21:25:31.271,INF] "RUNTIME:"                ".NET 3.1.8" (64-bit)
[    1, 1,21:25:31.272,INF] "GC:"                     "3 GENERATIONS"
[    1, 1,21:25:31.272,INF] "LOGS:"                   "/var/log/eventstore"
[    1, 1,21:25:31.280,INF] MODIFIED OPTIONS:

	WHAT IF:                  True (Command Line)
	EXT IP:                   0.0.0.0 (Config File)
	INT IP:                   0.0.0.0 (Config File)

DEFAULT OPTIONS:

	CONFIG:                   /etc/eventstore/eventstore.conf (<DEFAULT>)
	HELP:                     False (<DEFAULT>)
	VERSION:                  False (<DEFAULT>)
	LOG:                      /var/log/eventstore (<DEFAULT>)
	LOG LEVEL:                Default (<DEFAULT>)
	START STANDARD PROJECTIONS: False (<DEFAULT>)
	DISABLE HTTP CACHING:     False (<DEFAULT>)
	HTTP PORT:                2113 (<DEFAULT>)
	ENABLE EXTERNAL TCP:      False (<DEFAULT>)
	INT TCP PORT:             1112 (<DEFAULT>)
	EXT TCP PORT:             1113 (<DEFAULT>)
	EXT HOST ADVERTISE AS:    <empty> (<DEFAULT>)
	ADVERTISE HOST TO CLIENT AS: <empty> (<DEFAULT>)
	ADVERTISE HTTP PORT TO CLIENT AS: 0 (<DEFAULT>)
	ADVERTISE TCP PORT TO CLIENT AS: 0 (<DEFAULT>)
	EXT TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
	HTTP PORT ADVERTISE AS:   0 (<DEFAULT>)
	INT HOST ADVERTISE AS:    <empty> (<DEFAULT>)
	INT TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
	INT TCP HEARTBEAT TIMEOUT: 700 (<DEFAULT>)
	EXT TCP HEARTBEAT TIMEOUT: 1000 (<DEFAULT>)
	INT TCP HEARTBEAT INTERVAL: 700 (<DEFAULT>)
	EXT TCP HEARTBEAT INTERVAL: 2000 (<DEFAULT>)
	GOSSIP ON SINGLE NODE:    False (<DEFAULT>)
	CONNECTION PENDING SEND BYTES THRESHOLD: 10485760 (<DEFAULT>)
	CONNECTION QUEUE SIZE THRESHOLD: 50000 (<DEFAULT>)
	CLUSTER SIZE:             1 (<DEFAULT>)
	NODE PRIORITY:            0 (<DEFAULT>)
	MIN FLUSH DELAY MS:       2 (<DEFAULT>)
	COMMIT COUNT:             -1 (<DEFAULT>)
	PREPARE COUNT:            -1 (<DEFAULT>)
	DISABLE ADMIN UI:         False (<DEFAULT>)
	DISABLE STATS ON HTTP:    False (<DEFAULT>)
	DISABLE GOSSIP ON HTTP:   False (<DEFAULT>)
	DISABLE SCAVENGE MERGING: False (<DEFAULT>)
	SCAVENGE HISTORY MAX AGE: 30 (<DEFAULT>)
	DISCOVER VIA DNS:         True (<DEFAULT>)
	CLUSTER DNS:              fake.dns (<DEFAULT>)
	CLUSTER GOSSIP PORT:      2113 (<DEFAULT>)
	GOSSIP SEED:              <empty> (<DEFAULT>)
	STATS PERIOD SEC:         30 (<DEFAULT>)
	CACHED CHUNKS:            -1 (<DEFAULT>)
	READER THREADS COUNT:     4 (<DEFAULT>)
	CHUNKS CACHE SIZE:        536871424 (<DEFAULT>)
	MAX MEM TABLE SIZE:       1000000 (<DEFAULT>)
	HASH COLLISION READ LIMIT: 100 (<DEFAULT>)
	DB:                       /var/lib/eventstore (<DEFAULT>)
	INDEX:                    <empty> (<DEFAULT>)
	MEM DB:                   False (<DEFAULT>)
	SKIP DB VERIFY:           False (<DEFAULT>)
	WRITE THROUGH:            False (<DEFAULT>)
	UNBUFFERED:               False (<DEFAULT>)
	CHUNK INITIAL READER COUNT: 5 (<DEFAULT>)
	RUN PROJECTIONS:          None (<DEFAULT>)
	PROJECTION THREADS:       3 (<DEFAULT>)
	WORKER THREADS:           5 (<DEFAULT>)
	PROJECTIONS QUERY EXPIRY: 5 (<DEFAULT>)
	FAULT OUT OF ORDER PROJECTIONS: False (<DEFAULT>)
	ENABLE TRUSTED AUTH:      False (<DEFAULT>)
	TRUSTED ROOT CERTIFICATES PATH: <empty> (<DEFAULT>)
	CERTIFICATE FILE:         <empty> (<DEFAULT>)
	CERTIFICATE PRIVATE KEY FILE: <empty> (<DEFAULT>)
	CERTIFICATE PASSWORD:     <empty> (<DEFAULT>)
	CERTIFICATE STORE LOCATION: <empty> (<DEFAULT>)
	CERTIFICATE STORE NAME:   <empty> (<DEFAULT>)
	CERTIFICATE SUBJECT NAME: <empty> (<DEFAULT>)
	CERTIFICATE RESERVED NODE COMMON NAME: eventstoredb-node (<DEFAULT>)
	CERTIFICATE THUMBPRINT:   <empty> (<DEFAULT>)
	DISABLE INTERNAL TCP TLS: False (<DEFAULT>)
	DISABLE EXTERNAL TCP TLS: False (<DEFAULT>)
	AUTHORIZATION TYPE:       internal (<DEFAULT>)
	AUTHENTICATION TYPE:      internal (<DEFAULT>)
	AUTHORIZATION CONFIG:     <empty> (<DEFAULT>)
	AUTHENTICATION CONFIG:    <empty> (<DEFAULT>)
	DISABLE FIRST LEVEL HTTP AUTHORIZATION: False (<DEFAULT>)
	PREPARE TIMEOUT MS:       2000 (<DEFAULT>)
	COMMIT TIMEOUT MS:        2000 (<DEFAULT>)
	WRITE TIMEOUT MS:         2000 (<DEFAULT>)
	UNSAFE DISABLE FLUSH TO DISK: False (<DEFAULT>)
	UNSAFE IGNORE HARD DELETE: False (<DEFAULT>)
	SKIP INDEX VERIFY:        False (<DEFAULT>)
	INDEX CACHE DEPTH:        16 (<DEFAULT>)
	OPTIMIZE INDEX MERGE:     False (<DEFAULT>)
	GOSSIP INTERVAL MS:       2000 (<DEFAULT>)
	GOSSIP ALLOWED DIFFERENCE MS: 60000 (<DEFAULT>)
	GOSSIP TIMEOUT MS:        2500 (<DEFAULT>)
	READ ONLY REPLICA:        False (<DEFAULT>)
	UNSAFE ALLOW SURPLUS NODES: False (<DEFAULT>)
	ENABLE HISTOGRAMS:        False (<DEFAULT>)
	LOG HTTP REQUESTS:        False (<DEFAULT>)
	LOG FAILED AUTHENTICATION ATTEMPTS: False (<DEFAULT>)
	ALWAYS KEEP SCAVENGED:    False (<DEFAULT>)
	SKIP INDEX SCAN ON READS: False (<DEFAULT>)
	REDUCE FILE CACHE PRESSURE: False (<DEFAULT>)
	INITIALIZATION THREADS:   1 (<DEFAULT>)
	MAX AUTO MERGE INDEX LEVEL: 2147483647 (<DEFAULT>)
	WRITE STATS TO DB:        False (<DEFAULT>)
	MAX TRUNCATION:           268435456 (<DEFAULT>)
	MAX APPEND SIZE:          1048576 (<DEFAULT>)
	INSECURE:                 False (<DEFAULT>)
	ENABLE ATOM PUB OVER HTTP: False (<DEFAULT>)
	DEAD MEMBER REMOVAL PERIOD SEC: 1800 (<DEFAULT>)
```
:::

