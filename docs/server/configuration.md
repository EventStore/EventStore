# Options and configuration

EventStoreDB has a number of configuration options that can be changed. You can find all the options described in details in this section.

When you don't change the configuration, EventStoreDB will use sensible defaults, but they might not suit your needs. You can always instruct EventStoreDB to use a different set of options. There are multiple ways to configure EventStoreDB server, described below.

## Version and help

You can check what version of EventStoreDB you have installed by using the `--version` parameter in the command line. For example:

```
$ eventstored --version
EventStore version 5.0.11.0 (HEAD/efead54643bee3259391c2fa9dbe771b6aeeee88, Tue, 17 Aug 2021 16:15:36 +0200)
```

The full list of available options is available from the currently installed server by using the `--help` option in the command line.

## Configuration file

You would use the configuration file when you want the server to run with the same set of options every time. YAML files are better for large installations as you can centrally distribute and manage them, or generate them from a configuration management system.

The configuration file has YAML-compatible format. The basic format of the YAML configuration file is as follows:

```yaml
 ---
 Log: "/v5/logs"
 IntHttpPort: 2111
 --- 
```

:::tip
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

::: tabs#os
@tab linux
```
eventstore --log /tmp/eventstore/logs
```
@tab Windows
```
EventStore.ClusterNode.exe --log C:\Temp\EventStore\Logs
```
:::

## Testing the configuration

If more than one method is used to configure the server, it might be hard to find out what the effective configuration will be when the server starts. You can use the `--what-if` option for that purpose. 

When you run EventStoreDB with this option, it will print out the effective configuration applied from all available sources (default and custom configuration file, environment variables and command line parameters) and print it out to the console.

::: warning
Always run EventStoreDB as a service first, so it creates all the necessary directories using the service permissions. Running the service executable with `--what-if` option before starting the service _will create the data and log directories_ owned by the current user. It might prevent the service from running properly due to lack of write permissions for those directories.
:::

::: details Click here to see a WhatIf example
```
$ eventstored --what-if
[00001,01,09:35:32.957]
"ES VERSION:"             "5.0.11.0" ("HEAD"/"efead54643bee3259391c2fa9dbe771b6aeeee88", "Tue, 17 Aug 2021 16:15:36 +0200")
[00001,01,09:35:32.981] "OS:"                     Linux (Unix 4.19.128.0)
[00001,01,09:35:32.987] "RUNTIME:"                "5.16.0.220 (tarball Mon Nov 26 17:17:59 UTC 2018)" (64-bit)
[00001,01,09:35:32.987] "GC:"                     "2 GENERATIONS"
[00001,01,09:35:32.987] "LOGS:"                   "/var/log/eventstore"
MODIFIED OPTIONS:

        WHAT IF:                  True (Command Line)
        CLUSTER GOSSIP PORT:      2112 (Environment Variable)
        INT IP:                   0.0.0.0 (Config File)
        EXT IP:                   0.0.0.0 (Config File)
        INT HTTP PREFIXES:        http://*:2112/ (Config File)
        EXT HTTP PREFIXES:        http://*:2113/ (Config File)
        ADD INTERFACE PREFIXES:   false (Config File)
        RUN PROJECTIONS:          All (Config File)

DEFAULT OPTIONS:

        CONFIG:                   /etc/eventstore/eventstore.conf (<DEFAULT>)
        HELP:                     False (<DEFAULT>)
        VERSION:                  False (<DEFAULT>)
        LOG:                      /var/log/eventstore (<DEFAULT>)
        DEFINES:                  <empty> (<DEFAULT>)
        START STANDARD PROJECTIONS: False (<DEFAULT>)
        DISABLE HTTP CACHING:     False (<DEFAULT>)
        MONO MIN THREADPOOL SIZE: 10 (<DEFAULT>)
        INT HTTP PORT:            2112 (<DEFAULT>)
        EXT HTTP PORT:            2113 (<DEFAULT>)
        INT TCP PORT:             1112 (<DEFAULT>)
        INT SECURE TCP PORT:      0 (<DEFAULT>)
        EXT TCP PORT:             1113 (<DEFAULT>)
        EXT SECURE TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
        EXT SECURE TCP PORT:      0 (<DEFAULT>)
        EXT IP ADVERTISE AS:      <empty> (<DEFAULT>)
        EXT TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
        EXT HTTP PORT ADVERTISE AS: 0 (<DEFAULT>)
        INT IP ADVERTISE AS:      <empty> (<DEFAULT>)
        INT SECURE TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
        INT TCP PORT ADVERTISE AS: 0 (<DEFAULT>)
        INT HTTP PORT ADVERTISE AS: 0 (<DEFAULT>)
        INT TCP HEARTBEAT TIMEOUT: 700 (<DEFAULT>)
        EXT TCP HEARTBEAT TIMEOUT: 1000 (<DEFAULT>)
        INT TCP HEARTBEAT INTERVAL: 700 (<DEFAULT>)
        EXT TCP HEARTBEAT INTERVAL: 2000 (<DEFAULT>)
        GOSSIP ON SINGLE NODE:    False (<DEFAULT>)
        CONNECTION PENDING SEND BYTES THRESHOLD: 10485760 (<DEFAULT>)
        CONNECTION QUEUE SIZE THRESHOLD: 50000 (<DEFAULT>)
        STREAM INFO CACHE CAPACITY: 100000 (<DEFAULT>)
        FORCE:                    False (<DEFAULT>)
        CLUSTER SIZE:             1 (<DEFAULT>)
        NODE PRIORITY:            0 (<DEFAULT>)
        MIN FLUSH DELAY MS:       2 (<DEFAULT>)
        COMMIT COUNT:             -1 (<DEFAULT>)
        PREPARE COUNT:            -1 (<DEFAULT>)
        ADMIN ON EXT:             True (<DEFAULT>)
        STATS ON EXT:             True (<DEFAULT>)
        GOSSIP ON EXT:            True (<DEFAULT>)
        DISABLE SCAVENGE MERGING: False (<DEFAULT>)
        SCAVENGE HISTORY MAX AGE: 30 (<DEFAULT>)
        DISCOVER VIA DNS:         True (<DEFAULT>)
        CLUSTER DNS:              fake.dns (<DEFAULT>)
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
        PROJECTION THREADS:       3 (<DEFAULT>)
        WORKER THREADS:           5 (<DEFAULT>)
        PROJECTIONS QUERY EXPIRY: 5 (<DEFAULT>)
        FAULT OUT OF ORDER PROJECTIONS: False (<DEFAULT>)
        ENABLE TRUSTED AUTH:      False (<DEFAULT>)
        CERTIFICATE STORE LOCATION: <empty> (<DEFAULT>)
        CERTIFICATE STORE NAME:   <empty> (<DEFAULT>)
        CERTIFICATE SUBJECT NAME: <empty> (<DEFAULT>)
        CERTIFICATE THUMBPRINT:   <empty> (<DEFAULT>)
        CERTIFICATE FILE:         <empty> (<DEFAULT>)
        CERTIFICATE PASSWORD:     <empty> (<DEFAULT>)
        USE INTERNAL SSL:         False (<DEFAULT>)
        DISABLE INSECURE TCP:     False (<DEFAULT>)
        SSL TARGET HOST:          n/a (<DEFAULT>)
        SSL VALIDATE SERVER:      True (<DEFAULT>)
        AUTHENTICATION TYPE:      internal (<DEFAULT>)
        AUTHENTICATION CONFIG:    <empty> (<DEFAULT>)
        DISABLE FIRST LEVEL HTTP AUTHORIZATION: False (<DEFAULT>)
        PREPARE TIMEOUT MS:       2000 (<DEFAULT>)
        COMMIT TIMEOUT MS:        2000 (<DEFAULT>)
        UNSAFE DISABLE FLUSH TO DISK: False (<DEFAULT>)
        BETTER ORDERING:          False (<DEFAULT>)
        UNSAFE IGNORE HARD DELETE: False (<DEFAULT>)
        SKIP INDEX VERIFY:        False (<DEFAULT>)
        INDEX CACHE DEPTH:        16 (<DEFAULT>)
        OPTIMIZE INDEX MERGE:     False (<DEFAULT>)
        GOSSIP INTERVAL MS:       1000 (<DEFAULT>)
        GOSSIP ALLOWED DIFFERENCE MS: 60000 (<DEFAULT>)
        GOSSIP TIMEOUT MS:        500 (<DEFAULT>)
        ENABLE HISTOGRAMS:        False (<DEFAULT>)
        LOG HTTP REQUESTS:        False (<DEFAULT>)
        LOG FAILED AUTHENTICATION ATTEMPTS: False (<DEFAULT>)
        ALWAYS KEEP SCAVENGED:    False (<DEFAULT>)
        SKIP INDEX SCAN ON READS: False (<DEFAULT>)
        REDUCE FILE CACHE PRESSURE: False (<DEFAULT>)
        INITIALIZATION THREADS:   1 (<DEFAULT>)
        STRUCTURED LOG:           True (<DEFAULT>)
        MAX AUTO MERGE INDEX LEVEL: 2147483647 (<DEFAULT>)
        WRITE STATS TO DB:        True (<DEFAULT>)

[00001,01,09:35:32.999] {"defaults":{"Config":"\/etc\/eventstore\/eventstore.conf","Help":"False","Version":"False","Log":"\/var\/log\/eventstore","Defines":"System.String[]","StartStandardProjections":"False","DisableHTTPCaching":"False","MonoMinThreadpoolSize":"10","IntHttpPort":"2112","ExtHttpPort":"2113","IntTcpPort":"1112","IntSecureTcpPort":"0","ExtTcpPort":"1113","ExtSecureTcpPortAdvertiseAs":"0","ExtSecureTcpPort":"0","ExtIpAdvertiseAs":null,"ExtTcpPortAdvertiseAs":"0","ExtHttpPortAdvertiseAs":"0","IntIpAdvertiseAs":null,"IntSecureTcpPortAdvertiseAs":"0","IntTcpPortAdvertiseAs":"0","IntHttpPortAdvertiseAs":"0","IntTcpHeartbeatTimeout":"700","ExtTcpHeartbeatTimeout":"1000","IntTcpHeartbeatInterval":"700","ExtTcpHeartbeatInterval":"2000","GossipOnSingleNode":"False","ConnectionPendingSendBytesThreshold":"10485760","ConnectionQueueSizeThreshold":"50000","StreamInfoCacheCapacity":"100000","Force":"False","ClusterSize":"1","NodePriority":"0","MinFlushDelayMs":"2","CommitCount":"-1","PrepareCount":"-1","AdminOnExt":"True","StatsOnExt":"True","GossipOnExt":"True","DisableScavengeMerging":"False","ScavengeHistoryMaxAge":"30","DiscoverViaDns":"True","ClusterDns":"fake.dns","GossipSeed":"System.Net.IPEndPoint[]","StatsPeriodSec":"30","CachedChunks":"-1","ReaderThreadsCount":"4","ChunksCacheSize":"536871424","MaxMemTableSize":"1000000","HashCollisionReadLimit":"100","Db":"\/var\/lib\/eventstore","Index":null,"MemDb":"False","SkipDbVerify":"False","WriteThrough":"False","Unbuffered":"False","ChunkInitialReaderCount":"5","ProjectionThreads":"3","WorkerThreads":"5","ProjectionsQueryExpiry":"5","FaultOutOfOrderProjections":"False","EnableTrustedAuth":"False","CertificateStoreLocation":"","CertificateStoreName":"","CertificateSubjectName":"","CertificateThumbprint":"","CertificateFile":"","CertificatePassword":"","UseInternalSsl":"False","DisableInsecureTCP":"False","SslTargetHost":"n\/a","SslValidateServer":"True","AuthenticationType":"internal","AuthenticationConfig":"","DisableFirstLevelHttpAuthorization":"False","PrepareTimeoutMs":"2000","CommitTimeoutMs":"2000","UnsafeDisableFlushToDisk":"False","BetterOrdering":"False","UnsafeIgnoreHardDelete":"False","SkipIndexVerify":"False","IndexCacheDepth":"16","OptimizeIndexMerge":"False","GossipIntervalMs":"1000","GossipAllowedDifferenceMs":"60000","GossipTimeoutMs":"500","EnableHistograms":"False","LogHttpRequests":"False","LogFailedAuthenticationAttempts":"False","AlwaysKeepScavenged":"False","SkipIndexScanOnReads":"False","ReduceFileCachePressure":"False","InitializationThreads":"1","StructuredLog":"True","MaxAutoMergeIndexLevel":"2147483647","WriteStatsToDb":"True"},"modified":{"WhatIf":"True","ClusterGossipPort":"2112","IntIp":"0.0.0.0","ExtIp":"0.0.0.0","IntHttpPrefixes":"http:\/\/*:2112\/","ExtHttpPrefixes":"http:\/\/*:2113\/","AddInterfacePrefixes":"false","RunProjections":"All"}}
Exiting with exit code: 0.
Exit reason: WhatIf option specified
[00001,01,09:35:32.999] Exiting with exit code: 0.
Exit reason: "WhatIf option specified"
```
:::

