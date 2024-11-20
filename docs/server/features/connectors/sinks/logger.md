---
title: 'Logger Sink'
order: 5
---

## Overview

The logger sink logs a message about the connector and record details.

## Quickstart

You can create the Logger Sink connector as follows:

::: tabs
@tab Powershell

```powershell
$JSON = @"
{
  "settings": {
    "instanceTypeName": "logger-sink",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "streamId",
    "subscription:filter:expression": "example-stream"
  }
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -d $JSON `
  http://localhost:2113/connectors/logger-sink-connector
```

@tab Bash

```bash
JSON='{
  "settings": {
    "instanceTypeName": "logger-sink",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "streamId",
    "subscription:filter:expression": "example-stream"
  }
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -d "$JSON" \
  http://localhost:2113/connectors/logger-sink-connector
```

:::

After creating and starting the logger sink connector, every time an event is
appended to the `example-stream`, the Logger sink connector will print the
record to the console. You can find a list of available management API endpoints
in the [API Reference](../manage.md).

Here is an example:

```
logger-sink-connector RECORD WRITTEN: 78689502-9502-9502-9502-172978689502 example-stream@16437 eventType1
logger-sink-connector RECORD WRITTEN: 78699411-9411-9411-9411-172978699411 example-stream@18975 eventType2
logger-sink-connector RECORD WRITTEN: 78699614-9614-9614-9614-172978699614 example-stream@19142 eventType3
logger-sink-connector RECORD WRITTEN: 78699732-9732-9732-9732-172978699732 example-stream@21170 eventType4
logger-sink-connector RECORD WRITTEN: 78699899-9899-9899-9899-172978699899 example-stream@23711 eventType5
```

## Settings

Adjust these settings to specify the behavior and interaction of your logger sink connector with EventStoreDB, ensuring it operates according to your requirements and preferences.

::: tip
The logger sink inherits a set of common settings that are used to configure the connector. The settings can be found in
the [Sink Options](../settings.md#sink-options) page.
:::

The Logger sink can be configured with the following options:

| Name       | Details                                                                                                                                                                                                     |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `logLevel` | **Type**: enum<br><br>**Description:** The log level used by the sink.<br><br>**Accepted Values:**<br>- `trace`, `debug`, `information`, `warning`, `error`, `critical`, `none`<br><br>**Default**: `debug` |
