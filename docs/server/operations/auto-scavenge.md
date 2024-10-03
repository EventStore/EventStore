---
order: 2
---

# Auto-Scavenge

<Badge type="info" vertical="middle" text="License Required"/>

The Auto-scavenge feature automatically schedules and coordinates _cluster scavenges_ which are composed of multiple _node scavenges_ run across all of the nodes in the cluster. The feature ensures that only one node scavenge is executed at a time in the cluster, and handles restarting or cancelling scavenges if a node is lost.

Cluster scavenges can be configured to run on a set schedule with a CRON expression, and cluster scavenges can be paused and resumed if needed.

The Auto-scavenge does its best not to run the scavenge process on the leader node to minimise the impact a scavenge can have on your cluster. It does this by executing the scavenge on all of the non-leader nodes first. Once all of the other nodes have been scavenged, the leader of the cluster will resign before executing its own scavenge.

### Configuration

You require a [license key](../quick-start/installation.md#license-keys) to use this feature.

Refer to the [configuration guide](../configuration/README.md) for configuration mechanisms other than YAML.

```yaml
AutoScavenge:
  Enabled: true
```

Once the feature is enabled the server should log a similar message to the one below:

```
[18304, 1,23:01:53.335,INF] "AutoScavenge" "24.10.0" plugin enabled.
```

::: note
Auto-scavenge cannot be enabled at the same time as `dev` mode
:::

### HTTP endpoints

The Auto-scavenge feature exposes the following HTTP endpoints:

* `GET /auto-scavenge/enabled`: Tells if the Auto-scavenge feature is enabled or not.
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

#### Auto-Scavenge not started

The feature has to be configured and the server must not be running in `dev` mode for the feature to start.
If you see the following log it means the feature was not started:

```
[ 5104, 1,19:03:13.807,INF] "AutoScavenge" "24.10.0" plugin disabled. "Set 'EventStore:AutoScavenge:Enabled' to 'true' and don't run the server in dev mode to enable the auto-scavenge plugin"
```

Ensure the feature is enabled as described above.

When the feature starts correctly you should see the following log:

```
[11212, 1,18:44:34.070,INF] "AutoScavenge" "24.10.0" plugin enabled.
```