---
title: "Integrations"
order: 3
---

# Monitoring integrations

EventStoreDB supports several methods to integrate with external monitoring and observability tools. Those include:

- [Prometheus](#prometheus): collect metrics in Prometheus
- [Datadog](#datadog): monitor and measure the cluster with Datadog
- [ElasticSearch](#elastic): this section describes how to collect EventStoreDB logs in ElasticSearch
- [Vector](#vector): collect metrics and logs to your APM tool using Vector

## Vector

> Vector is a lightweight and ultra-fast tool for building observability pipelines.
> (from Vector website)

You can use [Vector] for extracting metrics or logs from your self-managed EventStore server.

It's also possible to collect metrics from the Event Store Cloud managed cluster or instance, as long as the
Vector agent is running on a machine that has a direct connection to the EventStoreDB server. You cannot,
however, fetch logs from Event Store Cloud using your own Vector agent.

### Installation

Follow the [installation instructions](https://vector.dev/docs/setup/installation/) provided by Vector to
deploy the agent. You can deploy and run it on the same machine where you run EventStoreDB server. If you run
EventStoreDB in Kubernetes, you can run Vector as a sidecar for each of the EventStoreDB pods.

### Configuration

Each Vector instance needs to be configured with sources and sinks. When configured properly, it will collect
information from each source, apply the necessary transformation (if needed), and send the transformed
information to the configured sink.

[Vector] provides [many different sinks], you most probably will find your preferred monitoring platform among
those sinks.

#### Collecting metrics

There is an official [EventStoreDB source] that you can use to pull relevant metrics from your database.

Below you can find an example that you can use in your `vector.toml` configuration file:

```toml
[sources.eventstoredb_metrics]
type = "eventstoredb_metrics"
endpoint = "https://{hostname}:{http_port}/stats"
scrape_interval_secs = 3
```

Here `hostname` is the EventStoreDB node hostname or the cluster DNS name, and `http_port` is the configured
HTTP port, which is `2113` by default.

#### Collecting logs

To collect logs, you can use the [file source] and configure it to target EventStoreDB log file. For log
collection, Vector must run on the same machine as EventStoreDB server as it collects the logs from files on
the local file system.

```toml
[sources.eventstoredb_logs]
type = "file"
# If you changed the default log location, please update the filepath accordingly.
include = ["/var/log/eventstore"]
read_from = "end"
```

#### Example

In this example, Vector runs on the same machine as EventStoreDB, collects metrics and logs, and then sends them to Datadog. Notice that despite the EventStoreDB HTTP is, in theory, accessible via `localhost`, it won't work if the server SSL certificate doesn't have `localhost` in the certificate CN or SAN.

```toml
[sources.eventstoredb_metrics]
type = "eventstoredb_metrics"
endpoint = "https://node1.esdb.acme.company:2113/stats"
scrape_interval_secs = 10

[sources.eventstoredb_logs]
type = "file"
include = ["/var/log/eventstore"]
read_from = "end"

[sinks.dd_metrics]
type = "datadog_metrics"
inputs = ["eventstoredb_metrics"]
api_key = "${DD_API_KEY}"
default_namespace = "service"

[sinks.dd_logs]
type = "datadog_logs"
inputs = ["sources.eventstoredb_logs"]
default_api_key = "${DD_API_KEY}"
compression = "gzip"
```

## Prometheus

You can collect EventStoreDB metrics to Prometheus and configure Grafana dashboards to monitor your deployment.

Event Store provides Prometheus support out of the box since version 23.6. Refer to [metrics](metrics.md) documentation to learn more.

## Datadog

Event Store doesn't provide Datadog integration out of the box, but you can use the community-supported
integration to collect EventStoreDB logs and metrics in Datadog.

Find out more details about the integration
in [Datadog documentation](https://docs.datadoghq.com/integrations/eventstore/).


[Vector]: https://vector.dev/docs/

[EventStoreDB source]: https://vector.dev/docs/reference/configuration/sources/eventstoredb_metrics/

[file source]: https://vector.dev/docs/reference/configuration/sources/file/

[many different sinks]: https://vector.dev/docs/reference/configuration/sinks/

[Console]: https://vector.dev/docs/reference/configuration/sinks/console/

## Elastic

Elastic Stack is one of the most popular tools for ingesting and analyzing logs and statistics:
- [Elasticsearch](https://www.elastic.co/guide/en/elasticsearch/reference/8.2/index.html) was built for advanced filtering and text analysis.
- [Filebeat](https://www.elastic.co/guide/en/beats/filebeat/8.2/index.html) allow tailing files efficiently.
- [Logstash](https://www.elastic.co/guide/en/logstash/current/getting-started-with-logstash.html) enables log transformations and processing pipelines.
- [Kibana](https://www.elastic.co/guide/en/kibana/8.2/index.html) is a dashboard and visualization UI for Elasticsearch data.

EventStoreDB exposes structured information through its logs and statistics, allowing straightforward integration with mentioned tooling.

### Logstash

Logstash is the plugin based data processing component of the Elastic Stack which sends incoming data to Elasticsearch. It's excellent for building a text-based processing pipeline. It can also gather logs from files (although Elastic recommends now Filebeat for that, see more in the following paragraphs). Logstash needs to either be installed on the EventStoreDB node or have access to logs storage. The processing pipeline can be configured through the configuration file (e.g. `logstash.conf`). This file contains the three essential building blocks:
- input - source of logs, e.g. log files, system output, Filebeat.
- filter - processing pipeline, e.g. to modify, enrich, tag log data,
- output - place where we'd like to put transformed logs. Typically that contains Elasticsearch configuration.

See the sample Logstash 8.2 configuration file. It shows how to take the EventStoreDB log files, split them based on the log type (regular and stats) and output them to separate indices to Elasticsearch:

```ruby
#######################################################
#  EventStoreDB logs file input
#######################################################
input {
  file {
    path => "/var/log/eventstore/*/log*.json"
    start_position => "beginning"
    codec => json
  }
}

#######################################################
#  Filter out stats from regular logs
#  add respecting field with log type
#######################################################
filter {
  # check if log path includes "log-stats"
  # so pattern for stats
  if [log][file][path] =~ "log-stats" {
    mutate {
      add_field => {
        "log_type" => "stats"
      }
    }
  }
  else {
    mutate {
      add_field => {
        "log_type" => "logs"
      }
    }
  }
}

#######################################################
#  Send logs to Elastic
#  Create separate indexes for stats and regular logs
#  using field defined in the filter transformation
#######################################################
output {
  elasticsearch {
    hosts => [ "elasticsearch:9200" ]
    index => 'eventstoredb-%{[log_type]}'
  }
}
```

You can play with such configuration through the [sample docker-compose](https://github.com/EventStore/samples/blob/2829b0a90a6488e1eee73fad0be33a3ded7d13d2/Logging/Elastic/Logstash/docker-compose.yml).

### Filebeat

Logstash was an initial attempt by Elastic to provide a log harvester tool. However, it appeared to have performance limitations. Elastic came up with the [Beats family](https://www.elastic.co/beats/), which allows gathering data from various specialized sources (files, metrics, network data, etc.). Elastic recommends Filebeat as the log collection and shipment tool off the host servers. Filebeat uses a backpressure-sensitive protocol when sending data to Logstash or Elasticsearch to account for higher volumes of data.

Filebeat can pipe logs directly to Elasticsearch and set up a Kibana data view.

Filebeat needs to either be installed on the EventStoreDB node or have access to logs storage. The processing pipeline can be configured through the configuration file (e.g. `filebeat.yml`). This file contains the three essential building blocks:
- input - configuration for file source, e.g. if stored in JSON format.
- output - place where we'd like to put transformed logs, e.g. Elasticsearch, Logstash,
- setup - additional setup and simple transformations (e.g. Elasticsearch indices template, Kibana data view).

See the sample Filebeat 8.2 configuration file. It shows how to take the EventStoreDB log files, output them to Elasticsearch prefixing index with `eventstoredb` and create a Kibana data view:

```yml
#######################################################
#  EventStoreDB logs file input
#######################################################
filebeat.inputs:
  - type: log
    paths:
      - /var/log/eventstore/*/log*.json
    json.keys_under_root: true
    json.add_error_key: true

#######################################################
#  ElasticSearch direct output
#######################################################
output.elasticsearch:
  index: "eventstoredb-%{[agent.version]}"
  hosts: ["elasticsearch:9200"]

#######################################################
#  ElasticSearch dashboard configuration
#  (index pattern and data view)
#######################################################
setup.dashboards:
  enabled: true
  index: "eventstoredb-*"

setup.template:
  name: "eventstoredb"
  pattern: "eventstoredb-%{[agent.version]}"

#######################################################
#  Kibana dashboard configuration
#######################################################
setup.kibana:
  host: "kibana:5601"

```

You can play with such configuration through the [sample docker-compose](https://github.com/EventStore/samples/blob/2829b0a90a6488e1eee73fad0be33a3ded7d13d2/Logging/Elastic/Filebeat/docker-compose.yml).

### Filebeat with Logstash

Even though Filebeat can pipe logs directly to Elasticsearch and do a basic Kibana setup, you'd like to have more control and expand the processing pipeline. That's why for production, it's recommended to use both. Multiple Filebeat instances (e.g. from different EventStoreDB clusters) can collect logs and pipe them to Logstash, which will play an aggregator role. Filebeat can output logs to Logstash, and Logstash can receive and process these logs with the Beats input. Logstash can transform and route logs to Elasticsearch instance(s).

In that configuration, Filebeat should be installed on the EventStoreDB node (or have access to file logs) and define Logstash as output. See the sample Filebeat 8.2 configuration file.

```yml
#######################################################
#  EventStoreDB logs file input
#######################################################
filebeat.inputs:
  - type: log
    paths:
      - /var/log/eventstore/*/log*.json
    json.keys_under_root: true
    json.add_error_key: true

#######################################################
#  Logstash output to transform and prepare logs
#######################################################
output.logstash:
  hosts: ["logstash:5044"]
```

Then the sample Logstash 8.2 configuration file will look like the below. It shows how to take the EventStoreDB logs from Filebeat, split them based on the log type (regular and stats) and output them to separate indices to Elasticsearch:

```ruby
#######################################################
#  Filebeat input 
#######################################################
input {
  beats {
    port => 5044
  }
}

#######################################################
#  Filter out stats from regular logs
#  add respecting field with log type
#######################################################
filter {
  # check if log path includes "log-stats"
  # so pattern for stats
  if [log][file][path] =~ "log-stats" {
    mutate {
      add_field => {
        "log_type" => "stats"
      }
    }
  }
  else {
    mutate {
      add_field => {
        "log_type" => "logs"
      }
    }
  }
}

#######################################################
#  Send logs to Elastic
#  Create separate indexes for stats and regular logs
#  using field defined in the filter transformation
#######################################################
output {
  elasticsearch {
    hosts => [ "elasticsearch:9200" ]
    index => 'eventstoredb-%{[log_type]}'
  }
}
```

You can play with such configuration through the [sample docker-compose](https://github.com/EventStore/samples/blob/2829b0a90a6488e1eee73fad0be33a3ded7d13d2/Logging/Elastic/FilebeatWithLogstash/docker-compose.yml).
