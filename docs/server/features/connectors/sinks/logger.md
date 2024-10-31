---
title: "Logger Sink"
order: 5
---

## Overview

The logger sink logs a message about the connector and record details and can be used for testing purposes.

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
RECORD WRITTEN: 78689502-9502-9502-9502-172978689502 example-stream:-1@16437 eventType1
RECORD WRITTEN: 78699411-9411-9411-9411-172978699411 example-stream:-1@18975 eventType2
RECORD WRITTEN: 78699614-9614-9614-9614-172978699614 example-stream:-1@19142 eventType3
RECORD WRITTEN: 78699732-9732-9732-9732-172978699732 example-stream:-1@21170 eventType4
RECORD WRITTEN: 78699899-9899-9899-9899-172978699899 example-stream:-1@23711 eventType5
```