# OTLP Exporter Plugin 

The OTLP Exporter Plugin allows you to export EventStoreDB metrics to a specified endpoint using the OpenTelemetry Protocol (OTLP). This guide will help you set up the exporter and customize its configuration so you can receive, process, and export and monitor metrics as desired. Learn more about the [OTEL collector here](https://opentelemetry.io/docs/collector/).

::: tip
The OTLP Exporter plugin is included in commercial versions of EventStoreDB.
:::


## Setting Up the OTLP Exporter
1. Download and install the plugin: First, download the OTLP Exporter plugin and place it in the `plugins` folder within your EventStoreDB installation directory. EventStoreDB will automatically recognize and use the plugin on its next startup.

After a successful installation, the server should log a message similar to the following:
```
[ 9408, 1,12:47:10.982,INF] Loaded SubsystemsPlugin plugin: "otlp-exporter" "24.2.0.0".
```
2. Configuration file setup: You must also provide a JSON configuration file. This file should be placed in the `config` folder inside your EventStoreDB installation directory. If the `config` folder doesn't exist, create it and add your configuration file there. 


## Configuration File Details 
The configuration file should specify: 

| Name     | Description                                                 |
|----------|-------------------------------------------------------------|
| Endpoint | Destination where the OTLP exporter will send the data.     |
| Headers  | Optional headers for the connection.                        |

Headers are key-value pairs separated by commas. To provide custom headers, you can add the `Headers` option in the JSON configuration file and set the value according to your requirements. For example:
```
"Headers": "api-key=value,other-config-value=value"
```
In the above example, we are adding two custom key value pairs to the `Headers` option.

## Example Configuration File
Here is the template for the JSON configuration file:
```
{
	"OpenTelemetry": {
		"Otlp": {
			"Endpoint": "http://localhost:4317",
			"Headers": ""
		}
	}
}
```

## Usage
To test the plugin, run an OTEL collector instance in Docker. You can then configure a monitoring tool (e.g., Prometheus, Jaeger, Zipkin) to scrape data from your endpoint. The flow is as follows:

```
EventStoreDB --> OTLP exporter --> OTEL collector --> Monitoring tool
```

EventStoreDB will log messages confirming the metrics export to your specified endpoint, such as: 
```
[27448, 1,10:34:03.283,INF] OtlpExporter: Exporting metrics to http://localhost:4317/ every 15.0 seconds
```

This indicates metrics are being exported to `http://localhost:4317/` every 15 seconds. You can adjust the scrape interval value in the `metricsconfig.json` file in the server installation directory:

```
"ExpectedScrapeIntervalSeconds": 15
```

## Troubleshooting

### Plugin Not Loaded
Ensure the plugin is correctly placed in the subdirectory within the `plugins` directory of your server's installation folder. If the `plugins` directory or the specific plugin subdirectory doesn't exist, you need to create them and place the plugin binaries there. To verify the plugin setup:

1. Go to the EventStoreDB installation directory where the executable is located. 
2. Ensure there's a directory called `plugins`. If not, create one. 
3. Inside `plugins`, create a subfolder for the OTLP Exporter Plugin (e.g., `EventStore.OtlpExporterPlugin` or any name you prefer), if it's missing.
4. Place the plugin binaries in this subfolder.

### EventStoreDB Metrics Not Exported to Endpoint
If EventStoreDB is not exporting metrics, check that the configuration file exists in the `config` folder and is correctly formatted. EventStoreDB will log a message if it can't find or recognize the configuration: 

```
[ 9408, 1,12:48:10.982,INF] OtlpExporter: No OpenTelemetry:Otlp configuration found. Not exporting metrics.
```
