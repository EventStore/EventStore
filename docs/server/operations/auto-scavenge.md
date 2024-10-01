---
order: 2
---

# Auto-Scavenge Plugin <Badge type="warning" vertical="middle" text="Commercial"/>

The Auto-scavenge plugin automatically schedules and coordinates _cluster scavenges_ which are composed of multiple _node scavenges_ run across all of the nodes in the cluster. The plugin ensures that only one node scavenge is executed at a time in the cluster, and handles restarting or cancelling scavenges if a node is lost.

Cluster scavenges can be configured to run on a set schedule with a CRON expression, and cluster scavenges can be paused and resumed if needed.

The Auto-scavenge does its best not to run the scavenge process on the leader node to minimise the impact a scavenge can have on your cluster. It does this by executing the scavenge on all of the non-leader nodes first. Once all of the other nodes have been scavenged, the leader of the cluster will resign before executing its own scavenge.

The Auto-scavenge plugin requires a valid license to use. If you attempt to start EventStoreDB with Auto-scavenge enabled and without a valid license, the process will log an error and exit.

### Enabling the plugin

By default, the Auto-scavenge plugin is bundled with EventStoreDB and located inside the `plugins` directory.

You then need to add the configuration to enable the plugin to a json file in the `{installation_directory}/config` directory.

For example, add the following configuration to `./config/auto-scavenge-plugin-config.json` file within the EventStoreDB installation directory:

```json
{
  "EventStore": {
      "Plugins": {
          "AutoScavenge": {
              "Enabled": true
          }
      }
  }
}
```

The name of the above json config file is not important. What is important is that the json config should be in the `{installation_directory}/config` directory.
Once the plugin is installed and enabled the server should log a similar message to the one below:

```
[18304, 1,23:01:53.335,INF] "AutoScavenge" "0.0.1" plugin enabled.
```

::: note
The Auto-scavenge plugin doesn't support a server running in `dev` mode
:::

### HTTP endpoints

The Auto-scavenge plugin exposes the following HTTP endpoints:

* `GET /auto-scavenge/enabled`: Tells if the Auto-scavenge plugin is enabled or not.
* `GET /auto-scavenge/status`: Tells the current status of the cluster auto-scavenge process. You can know for example if it's running or when the next process will run.
* `POST /auto-scavenge/configure`: Configure the cluster auto-scavenge process schedule. The expected json payload is the following:
```json
{
  "schedule": "{ Valid standard CRON expression }"
}
```
* `POST /auto-scavenge/pause`: Pauses the cluster auto-scavenge process. No payload is needed.
* `POST /auto-scavenge/resume`: Resumes the cluster auto-scavenge process. No payload is needed.

All of the endpoints require a user to be authenticated and to have either an ops or an admin role, with the exception of the `GET /auto-scavenge/enabled` endpoint which allows anonymous access.

### Troubleshooting

#### Plugin not loaded

The plugin has to be located in a subdirectory of the server's plugins directory. To check this:

1. Go to the installation directory of the server, the directory containing the EventStoreDB executable.
2. In this directory there should be a directory called plugins, create it if this is not the case.
3. The plugins directory should have a subdirectory for the plugin, for instance called `EventStore.AutoScavenge` but this could be any name. Create it if it doesn't exist.
4. The binaries of the plugin should be located in the subdirectory which was checked in the previous step.

You can verify which plugins have been found and loaded by looking for the following logs:

```
[11212, 1,18:44:30.420,INF] Plugins path: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins"
[11212, 1,18:44:30.420,INF] Adding: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins" to the plugin catalog.
[11212, 1,18:44:30.422,INF] Adding: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins\\EventStore.Licensing" to the plugin catalog.
[11212, 1,18:44:30.432,INF] Adding: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins\\EventStore.POC.ConnectedSubsystemsPlugin" to the plugin catalog.
[11212, 1,18:44:30.433,INF] Adding: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins\\EventStore.POC.ConnectorsPlugin" to the plugin catalog.
[11212, 1,18:44:30.436,INF] Adding: "C:\\EventStore.CoupledPlugins\\EventStore\\src\\EventStore.ClusterNode\\bin\\Debug\\net8.0\\plugins\\EventStore.TcpPlugin" to the plugin catalog.
[11212, 1,18:44:30.479,INF] Loaded SubsystemsPlugin plugin: "licensing" "24.6.0.0".
[11212, 1,18:44:30.483,INF] Loaded SubsystemsPlugin plugin: "connected" "0.0.5".
[11212, 1,18:44:30.496,INF] Loaded ConnectedSubsystemsPlugin plugin: "connectors" "0.0.5".
[11212, 1,18:44:30.496,INF] Loaded ConnectedSubsystemsPlugin plugin: "auto-scavenge" "0.0.1".
```

#### Plugin not started

The plugin has to be configured and the server must not be running in `dev` mode for the plugin to start.
If you see the following log it means the plugin was found but was not started:

```
[ 5104, 1,19:03:13.807,INF] "AutoScavenge" "0.0.1" plugin disabled. "Set 'EventStore:Plugins:AutoScavenge:Enabled' to 'true' and don't run the server in dev mode to enable the auto-scavenge plugin"
```

Ensure the plugin is enabled by adding a json config file to the `{installation_directory}/config` directory as mentioned above.
When the plugin starts correctly you should see the following log:

```
[11212, 1,18:44:34.070,INF] "AutoScavenge" "0.0.1" plugin enabled.
```