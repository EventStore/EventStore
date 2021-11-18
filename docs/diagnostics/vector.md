# Vector

> Vector is a lightweight and ultra-fast tool for building observability pipelines.
> (from Vector website)

You can use [Vector] for extracting metrics or logs from your self-managed EventStore server.

It's also possible to collect metrics from the Event Store Cloud managed cluster or instance, as long as the Vector agent is running on a machine that has a direct connection to the EventStoreDB server. You cannot, however, fetch logs from Event Store Cloud using your own Vector agent.

## Installation

Follow the [installation instructions](https://vector.dev/docs/setup/installation/) provided by Vector to deploy the agent. You can deploy and run it on the same machine where you run EventStoreDB server. If you run EventStoreDB in Kubernetes, you can run Vector as a sidecar for each of the EventStoreDB pods.

## Configuration

Each Vector instance needs to be configured with sources and sinks. When configured properly, it will collect information from each source, apply the necessary transformation (if needed), and send the transformed information to the configured sink.

[Vector] provides [many different sinks], you most probably will find your preferred monitoring platform among those sinks.

### Collecting metrics

There is an official [EventStoreDB source] that you can use to pull relevant metrics from your database.

Below you can find an example that you can use in your `vector.toml` configuration file:

```toml
[sources.eventstoredb_metrics]
type = "eventstoredb_metrics"
endpoint = "https://{hostname}:{http_port}/stats"
scrape_interval_secs = 3
```

Here `hostname` is the EventStoreDB node hostname or the cluster DNS name, and `http_port` is the configured HTTP port, which is `2113` by default. 

### Collecting logs

To collect logs, you can use the [file source] and configure it to target EventStoreDB log file. For log collection, Vector must run on the same machine as EventStoreDB server as it collects the logs from files on the local file system.

```toml
[sources.eventstoredb_logs]
type = "file"
# If you changed the default log location, please update the filepath accordingly.
include = [ "/var/log/eventstore" ]
read_from = "end"
```

### Example

In this example, Vector runs on a machine as EventStoreDB, collecting metrics and logs, then sending them to Datadog. Notice that despite the EventStoreDB HTTP is, in theory, accessible via `localhost`, it won't work if the server SSL certificate doesn't have `localhost` in the certificate CN or SAN.

```toml
[sources.eventstoredb_metrics]
type = "eventstoredb_metrics"
endpoint = "https://node1.esdb.acme.company:2113/stats"
scrape_interval_secs = 10

[sources.eventstoredb_logs]
type = "file"
include = [ "/var/log/eventstore" ]
read_from = "end"

[sinks.dd_metrics]
type = "datadog_metrics"
inputs = [ "eventstoredb_metrics" ]
api_key = "${DD_API_KEY}"
default_namespace = "service"

[sinks.dd_logs]
type = "datadog_logs"
inputs = [ "sources.eventstoredb_logs" ]
default_api_key = "${DD_API_KEY}"
compression = "gzip"
```

[Vector]: https://vector.dev/docs/
[EventStoreDB source]: https://vector.dev/docs/reference/configuration/sources/eventstoredb_metrics/
[file source]: https://vector.dev/docs/reference/configuration/sources/file/
[many different sinks]: https://vector.dev/docs/reference/configuration/sinks/
[Console]: https://vector.dev/docs/reference/configuration/sinks/console/
