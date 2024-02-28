# Logs Endpoint Plugin 
The Logs Endpoint plugin provides HTTP access to EventStoreDB log files, facilitating easy log management. 

::: tip
The Logs Endpoint plugin is included in commercial versions of EventStoreDB.
:::

## Enabling the Plugin

1. Download and install: First, download the plugin and place it in the `plugins` folder within your EventStoreDB installation directory. 
2. Start EventStoreDB: Upon starting, EventStoreDB automatically loads the plugin. 

After a successful installation, EventStoreDB logs a message indicating the logs are available:
```
[19676, 1,13:46:13.151,INF] LogsEndpoint: Serving logs from "<FULL-PATH-TO-LOGS>" at endpoint "/admin/logs"
```

## Accessing Logs

- **Endpoint URL**: Access the logs via `/admin/logs`on your server. Construct the full URL as follows: 
```
http(s)://< node ip or hostname >:<node port>/admin/logs
```
Example:
```
https://localhost:2113/admin/logs
```

## Access Restrictions

Only authenticated users belonging to the `$admins` or `$ops` groups can use this endpoint. 

## Using the Endpoint

Use `curl` for command-line access, and optionally `jq` for formatting the output.  

### Listing Log Files

To list current log files:
```bash
curl https://<USER>:<PASSWORD>@hostname-or-ip:2113/admin/logs | jq
```
Example:
```bash

curl https://user:password@localhost:2113/admin/logs | jq
```

Sample response:
```json
[
  {
    "name": "log-stats20240205.json",
    "lastModified": "2024-02-05T13:14:14.2789475+00:00",
    "size": 1058614
  },
  {
    "name": "log20240205.json",
    "lastModified": "2024-02-05T13:14:37.0781601+00:00",
    "size": 158542
  }
]
```

The response is ordered from most recent change first, is limited to a maximum of 1000 items, and includes:

| Name | Description |
|---|---|
| name | File name of the log file |
| createdAt | Timestamp of when the log file was created in [round-trip format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier) (local time with time zone information) |
| lastModified | Timestamp of the last modification in [round-trip format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier) (local time with time zone information) |
| size | Size of the log file in bytes |

### Downloading Log Files

To download a specific log file, append its name to the URL:
```bash
curl https://<USER>:<PASSWORD>@hostname-or-ip:2113/admin/logs/<name> --output <name>
```
Example:
```bash
curl https://user:password@localhost:2113/admin/logs/log20240205.json --output log20240205.json
```

## Verify Plugin Setup

To verify the plugin setup:

1. Go to the EventStoreDB installation directory where the executable is located. 
2. Ensure there's a directory called `plugins`. If not, create one. 
3. Inside `plugins`, create a subfolder for the Logs Endpoint Plugin (e.g., `EventStore.Diagnostics.LogsEndpointPlugin` or any name you prefer), if it's missing.
4. Place the plugin binaries in this subfolder.

## Troubleshooting

- **Plugin Not Loaded:** Ensure the plugin is correctly placed in the subdirectory within the `plugins` directory of your server's installation folder. If the `plugins` directory or the specific plugin subdirectory doesn't exist, you need to create them and place the plugin binaries there. 

- **404 Not Found:** Verify the plugin is loaded by checking the server startup logs.

- **401 Unauthorized:** Confirm the credentials are correct and the user belongs to the `$ops` or `$admins` group.

- **Log Files Directory Not Found:** Check the `NodeIp` and `NodePort` settings are current and not using deprecated settings like `HttpIp` or `HttpPort`.

