# Exposing endpoints

If you need to disable some of the HTTP endpoints on the external HTTP interface, you can change some of the settings below. It is possible to disable the Admin UI, stats and gossip port to be exposed externally.

You can disable the Admin UI on external HTTP by setting `AdminOnExt` setting to `false`.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--admin-on-ext` |
| YAML                 | `AdminOnExt` |
| Environment variable | `EVENTSTORE_ADMIN_ON_EXT` | 

**Default**: `true`, Admin UI is enabled on the external HTTP.

Exposing the `stats` endpoint externally is required for the Admin UI and can also be useful if you collect stats for an external monitoring tool.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--stats-on-ext` |
| YAML                 | `StatsOnExt` |
| Environment variable | `EVENTSTORE_STATS_ON_EXT` | 

**Default**: `true`, stats endpoint is enabled on the external HTTP.

You can also disable the gossip protocol in the external HTTP interface. If you do that, ensure that the internal interface is properly configured. Also, if you use [gossip with DNS](../clustering/using-dns.md), ensure that the [gossip port](../clustering/gossip.md#gossip-port) is set to the internal HTTP port.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--gossip-on-ext` |
| YAML                 | `GossipOnExt` |
| Environment variable | `EVENTSTORE_GOSSIP_ON_EXT` | 

**Default**: `true`, gossip is enabled on the external HTTP.
