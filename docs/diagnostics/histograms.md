# Histograms

Histograms give a distribution in percentiles of the time spent on several metrics. This can be used to diagnose issues in the system. It is not recommended to enable this in production environment. When enabled, histogram stats are available at their corresponding http endpoints.

For example, you could ask for a stream reader histograms like this:

```bash
curl http://localhost:2113/histogram/reader-streamrange -u admin:changeit
```

That would give a response with the stats distributed across histogram buckets:

```
   Value     Percentile TotalCount 1/(1-Percentile)

   0.022 0.000000000000          1           1.00
   0.044 0.100000000000         30           1.11
   0.054 0.200000000000         59           1.25
   0.074 0.300000000000         88           1.43
   0.092 0.400000000000        118           1.67
   0.108 0.500000000000        147           2.00
   0.113 0.550000000000        162           2.22
   0.127 0.600000000000        176           2.50
   0.140 0.650000000000        191           2.86
   0.155 0.700000000000        206           3.33
   0.168 0.750000000000        220           4.00
   0.179 0.775000000000        228           4.44
   0.197 0.800000000000        235           5.00
   0.219 0.825000000000        242           5.71
   0.232 0.850000000000        250           6.67
   0.277 0.875000000000        257           8.00
   0.327 0.887500000000        261           8.89
   0.346 0.900000000000        264          10.00
   0.522 0.912500000000        268          11.43
   0.836 0.925000000000        272          13.33
   0.971 0.937500000000        275          16.00
   1.122 0.943750000000        277          17.78
   1.153 0.950000000000        279          20.00
   1.217 0.956250000000        281          22.86
   2.836 0.962500000000        283          26.67
   2.972 0.968750000000        284          32.00
   3.607 0.971875000000        285          35.56
   4.964 0.975000000000        286          40.00
   8.536 0.978125000000        287          45.71
  11.035 0.981250000000        288          53.33
  11.043 0.984375000000        289          64.00
  11.043 0.985937500000        289          71.11
  34.013 0.987500000000        290          80.00
  34.013 0.989062500000        290          91.43
  41.812 0.990625000000        292         106.67
  41.812 0.992187500000        292         128.00
  41.812 0.992968750000        292         142.22
  41.812 0.993750000000        292         160.00
  41.812 0.994531250000        292         182.86
  41.812 0.995312500000        292         213.33
  41.812 0.996093750000        292         256.00
  41.812 0.996484375000        292         284.44
  41.878 0.996875000000        293         320.00
  41.878 1.000000000000        293

#[Mean = 0.854, StdDeviation = 4.739]
#[Max = 41.878, Total count = 293]
#[Buckets = 20, SubBuckets = 2048]
```

## Reading histograms

The histogram response tells you some useful metrics like mean, max, standard deviation and also that in 99% of cases reads take about 41.8ms, as in the example above.

## Using histograms

You can enable histograms in a development environment and run a specific task to see how it affects the database, telling you where and how the time is spent.

Execute a `GET` HTTP call to a cluster node using the `http://<node>:2113/histogram/<metric>` path to get a response. Here `2113` is the default external HTTP port.

## Available metrics

| Endpoint | Measures time spent |
| :------- | :---------- |
| `reader-streamrange` | `ReadStreamEventsForward` and `ReadStreamEventsBackwards` |
| `writer-flush` | Flushing to disk in the storage writer service |
| `chaser-wait` and `chaser-flush` | Storage chaser |
| `reader-readevent` | Checking the stream access and reading an event |
| `reader-allrange` | `ReadAllEventsForward` and `ReadAllEventsBackward` |
| `request-manager` | --- |
| `tcp-send` | Sending messages over TCP |
| `http-send` | Sending messages over HTTP |

## Enabling histograms

Use the option described below to enable histograms. Because collecting histograms uses CPU resources, they are disabled by default. 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--enable-histograms` |
| YAML                 | `EnableHistograms` |
| Environment variable | `EVENTSTORE_ENABLE_HISTOGRAMS` |

**Default**: `false`

