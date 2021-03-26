# Heartbeat timeouts

EventStoreDB uses heartbeats over all TCP connections to discover dead clients and nodes. Setting heartbeat timeouts requires thought. Set them too short and you receive false positives, set them too long and discovery of dead clients and nodes is slower.

Each heartbeat has two points of configuration. The first is the 'interval', this represents how often the system should consider a heartbeat. EventStoreDB doesn't send a heartbeat for every interval. EventStoreDB sends heartbeat requests if it has not heard from a node within the configured interval. On a busy cluster, you may never see any heartbeats.

The second point of configuration is the _timeout_. This determines how long EventStoreDB server waits for a client or node to respond to a heartbeat request.

Different environments need different values for these settings. While low numbers work well on a LAN they tend to not work well in the cloud. The defaults are likely fine on a LAN. If you experience frequent elections in your environment, you can try to increase both interval and timeout, for example:

- An interval of 5000ms.
- A timeout of 1000ms.

::: tip
If in doubt, choose higher numbers. This adds a small period of time to discover a dead client or node and is better than the alternative, which is false positives.
:::

Internal TCP heartbeat (between cluster nodes): 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-heartbeat-interval` |
| YAML                 | `IntTcpHeartbeatInterval` |
| Environment variable | `EVENTSTORE_INT_TCP_HEARTBEAT_INTERVAL` | 

**Default**: `700` (ms)

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-tcp-heartbeat-timeout` |
| YAML                 | `IntTcpHeartbeatTimeout` |
| Environment variable | `EVENTSTORE_INT_TCP_HEARTBEAT_TIMEOUT` | 

**Default**: `700` (ms)

External TCP heartbeat (between client and server): 

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-tcp-heartbeat-interval` |
| YAML                 | `ExtTcpHeartbeatInterval` |
| Environment variable | `EVENTSTORE_EXT_TCP_HEARTBEAT_INTERVAL` | 

**Default**: `2000` (ms)

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-tcp-heartbeat-timeout` |
| YAML                 | `ExtTcpHeartbeatTimeout` |
| Environment variable | `EVENTSTORE_EXT_TCP_HEARTBEAT_TIMEOUT` | 

**Default**: `1000` (ms)
