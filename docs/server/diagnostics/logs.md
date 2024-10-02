---
title: "Logs"
order: 1
---

# Database logs

EventStoreDB logs its internal operations to the console (stdout) and to log files. The default location of
the log files and the way to change it is described [below](#logs-location).

There are a few options to change the way how EventStoreDB produces logs and how detailed the logs should be.

::: warning
The EventStoreDB logs may contain sensitive information such as stream names, usernames, and projection definitions.
:::

## Log format

EventStoreDB uses the structured logging in JSON format that is more machine-friendly and can be ingested by
vendor-specific tools like Logstash or Datadog agent.

Here is how the structured log looks like:

```json
{
  "PID": "6940",
  "ThreadID": "23",
  "Date": "2020-06-16T16:14:02.052976Z",
  "Level": "Debug",
  "Logger": "ProjectionManager",
  "Message": "PROJECTIONS: Starting Projections Manager. (Node State : {state})",
  "EventProperties": {
    "state": "Master"
  }
}
{
  "PID": "6940",
  "ThreadID": "15",
  "Date": "2020-06-16T16:14:02.052976Z",
  "Level": "Info",
  "Logger": "ClusterVNodeController",
  "Message": "========== [{internalHttp}] Sub System '{subSystemName}' initialized.",
  "EventProperties": {
    "internalHttp": "127.0.0.1:2112",
    "subSystemName": "Projections"
  }
}
{
  "PID": "6940",
  "ThreadID": "23",
  "Date": "2020-06-16T16:14:02.052976Z",
  "Level": "Debug",
  "Logger": "MultiStreamMessageWriter",
  "Message": "PROJECTIONS: Resetting Worker Writer",
  "EventProperties": {}
}
{
  "PID": "6940",
  "ThreadID": "23",
  "Date": "2020-06-16T16:14:02.055000Z",
  "Level": "Debug",
  "Logger": "ProjectionCoreCoordinator",
  "Message": "PROJECTIONS: SubComponent Started: {subComponent}",
  "EventProperties": {
    "subComponent": "EventReaderCoreService"
  }
}
```

This format is aligned with [Serilog Compact JSON format](https://github.com/serilog/serilog-formatting-compact).

## Logs location

Log files are located in `/var/log/eventstore` for Linux and macOS, and in the `logs` subdirectory of the
EventStoreDB installation directory on Windows. You can change the log files location using the `Log`
configuration option.

::: tip
Moving logs to a separate storage might improve the database performance if you keep the default
verbose log level.
:::

| Format               | Syntax           |
|:---------------------|:-----------------|
| Command line         | `--log`          |
| YAML                 | `Log`            |
| Environment variable | `EVENTSTORE_LOG` |

For example, adding this line to the `eventstore.conf` file will force writing logs to
the `/tmp/eventstore/logs` directory:

```text:no-line-numbers
Log: /tmp/eventstore/logs
```

## Log level

You can change the level using the `LogLevel` setting:

| Format               | Syntax                 |
|:---------------------|:-----------------------|
| Command line         | `--log-level`          |
| YAML                 | `LogLevel`             |
| Environment variable | `EVENTSTORE_LOG_LEVEL` |

Acceptable values are: `Default`, `Verbose`, `Debug`, `Information`, `Warning`, `Error`, and `Fatal`.

## Logging options

You can tune the EventStoreDB logging further by using the logging options described below.

### Log configuration file

Specifies the location of the file which configures the logging levels of various components.

| Format               | Syntax                  |
|:---------------------|:------------------------|
| Command line         | `--log-config`          |
| YAML                 | `LogConfig`             |
| Environment variable | `EVENTSTORE_LOG_CONFIG` |

By default, the application directory (and `/etc/eventstore` on Linux and Mac) are checked. You may specify a
full path.

### HTTP requests logging

EventStoreDB can also log all the incoming HTTP requests, like many HTTP servers do. Requests are logged
before being processed, so unsuccessful requests are logged too.

Use one of the following ways to enable the HTTP requests logging:

| Format               | Syntax                         |
|:---------------------|:-------------------------------|
| Command line         | `--log-http-requests`          |
| YAML                 | `LogHttpRequests`              |
| Environment variable | `EVENTSTORE_LOG_HTTP_REQUESTS` |

**Default**: `false`, logging HTTP requests is disabled by default.

### Log failed authentication

For security monitoring, you can enable logging failed authentication attempts by
setting `LogFailedAuthenticationAttempts` setting to true.

| Format               | Syntax                                          |
|:---------------------|:------------------------------------------------|
| Command line         | `--log-failed-authentication-attempts`          |
| YAML                 | `LogFailedAuthenticationAttempts`               |
| Environment variable | `EVENTSTORE_LOG_FAILED_AUTHENTICATION_ATTEMPTS` |

**Default**: `false`

### Log console format

The format of the console logger. Use `Json` for structured log output.

| Format               | Syntax                          |
|:---------------------|:--------------------------------|
| Command line         | `--log-console-format`          |
| YAML                 | `LogConsoleFormat`              |
| Environment variable | `EVENTSTORE_LOG_CONSOLE_FORMAT` |

Acceptable values are: `Plain`, `Json`

**Default**: `Plain`

### Log file size

The maximum size of each log file, in bytes.

| Format               | Syntax                     |
|:---------------------|:---------------------------|
| Command line         | `--log-file-size`          |
| YAML                 | `LogFileSize`              |
| Environment variable | `EVENTSTORE_LOG_FILE_SIZE` |

**Default**: `1GB`

### Log file interval

How often to rotate logs.

| Format               | Syntax                         |
|:---------------------|:-------------------------------|
| Command line         | `--log-file-interval`          |
| YAML                 | `LogFileInterval`              |
| Environment variable | `EVENTSTORE_LOG_FILE_INTERVAL` |

Acceptable values are: `Minute`, `Hour`, `Day`, `Week`, `Month`, `Year`

**Default**: `Day`

### Log file retention count

Defines how many log files need to be kept on disk. By default, logs for the last month are available. Tune this setting if you need to have more history in the logs, or you need to save disk space. 

| Format               | Syntax                           |
|:---------------------|:---------------------------------|
| Command line         | `--log-file-retention-count`     |
| YAML                 | `LogFileRetentionCount`          |
| Environment variable | `EVENTSTORE_LOG_RETENTION_COUNT` |

**Default**: `31`

#### Disable log file

You can completely disable logging to a file by changing the `DisableLogFile` option.

| Format               | Syntax                        |
|:---------------------|:------------------------------|
| Command line         | `--disable-log-file`          |
| YAML                 | `DisableLogFile`              |
| Environment variable | `EVENTSTORE_DISABLE_LOG_FILE` |

**Default**: `false`

## Logs download

<Badge type="warning" vertical="middle" text="License Required"/>

The _Logs Download Plugin_ provides HTTP access to EventStoreDB logs so that they can be viewed without requiring file system access.

You require a [license key](../configuration/license-keys.md) to use this plugin.

::: tip
You can use this API to download log files from your managed EventStoreDB clusters in Event Store Cloud.
:::

On startup the server will log a message similar to:
```:no-line-numbers
LogsEndpoint: Serving logs from "<FULL-PATH-TO-LOGS>" at endpoint "/admin/logs"
```

Access the logs via the `/admin/logs` endpoint on your server. Construct the full URL as follows:

```:no-line-numbers
http(s)://<node ip or hostname>:<node port>/admin/logs
```

Example:
```:no-line-numbers
https://localhost:2113/admin/logs
```

Only authenticated users belonging to the `$admins` or `$ops` groups can use this endpoint.

### Listing log files

To list the current log files, issue a `GET` to the `/admin/logs` endpoint

Example:
```bash:no-line-numbers
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

| Name         | Description                                                                                                                                                                                                                                       |
|--------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| name         | File name of the log file                                                                                                                                                                                                                         |
| createdAt    | Timestamp of when the log file was created in [round-trip format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier) (local time with time zone information) |
| lastModified | Timestamp of the last modification in [round-trip format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier) (local time with time zone information)         |
| size         | Size of the log file in bytes                                                                                                                                                                                                                     |

### Downloading log files

To download a specific log file, append its name to the URL:

Example:
```bash:no-line-numbers
curl https://user:password@localhost:2113/admin/logs/log20240205.json --output log20240205.json
```

### Troubleshooting

- **404 Not Found:** Verify the plugin is loaded by checking the server startup logs.

- **401 Unauthorized:** Confirm the credentials are correct and the user belongs to the `$ops` or `$admins` group.

- **Log Files Directory Not Found:** Check the `NodeIp` and `NodePort` settings are current and not using the deprecated settings `HttpIp` or `HttpPort`.
