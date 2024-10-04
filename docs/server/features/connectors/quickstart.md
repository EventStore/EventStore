---
order: 1
---

# Quick start

The Connectors plugin is pre-installed in the commercial edition. The first version of EventStoreDB to support Connectors is **v24.2**.

## Enable the plugin

Refer to the general [plugins configuration](../../configuration/plugins.md) guide to see how to configure plugins with JSON files and environment variables.

Sample JSON configuration:

Let's create a connector and send events to it.
```json
{
  "EventStore": {
    "Plugins": {
      "Connectors": {
        "Enabled": true
      }
    }
  }
}
```

Alternatively, set the environment variable `EVENTSTORE__PLUGINS__CONNECTORS__ENABLED` to `true`.

Let's create a connector and send events to it.

## Set up an external system

For example, create a `public bin` by visiting [Requestbin](https://public.requestbin.com/r). This is only suitable for test data. It will present you with a unique endpoint such as: `https://enkb1keveb5r.x.pipedream.net`.

When you create a public request bin, it will start waiting for requests. You can then use the bin URL as the sink endpoint for the connector.

## Create a connector instance

Use `curl` or a similar utility to issue a `POST` request as follows. This will create a connector instance called `my-connector`, configure it to send events to our external system, and enable the connector instance.

::: tabs#shell
@tab Powershell
```powershell
$JSON = @'
{
  "Sink": "https://enkb1keveb5r.x.pipedream.net"
}
'@ -replace '"', '\"'

curl.exe -i                           `
  -H "Content-Type: application/json" `
  -u "admin:changeit"                 `
  -d $JSON                            `
  https://localhost:2113/connectors/my-connector
```
@tab Bash
```bash
export json="{
  \"Sink\": \"https://enkb1keveb5r.x.pipedream.net\"
}"

curl -i \
  -H "Content-Type: application/json" \
  -u "admin:changeit" \
  -d $json \
  https://localhost:2113/connectors/my-connector
```
:::

- Ensure you pass the correct credentials for the `-u` flag. Only administrators can create connectors.
- The sink URL is where the sink will POST to. Adjust it to be your own URL created in the first step.
- Ensure to use the correct URL for your EventStoreDB instance or cluster.

## Append an event

Visit the EventStoreDB web UI and append an event to a stream via the Stream Browser. You will find the `Add Event` button in the top right corner of the Stream Browser.
Appending a new event will trigger the connector to send the event to the sink.

![Create Event](./images/create-event.png)

## Check the event was received

Visit the public bin webpage and check that the event was received.

![View Received Event](./images/receive-event.png)

Congratulations! You have successfully set up and used the Connectors functionality in EventStoreDB.
