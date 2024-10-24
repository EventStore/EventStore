---
Title: "Common settings"
Order: 3
---

# Common settings

All sinks share a common set of configuration options that can be used to
customize their behavior.

## Instance configuration

| Name               | Details                                                                                                        |
| ------------------ | -------------------------------------------------------------------------------------------------------------- |
| `InstanceTypeName` | _required_<br><br>**Type**: string<br><br>**Description:** The name of the instance type for the sink.<br><br> |

## Subscription configuration

| Name                             | Details                                                                                                                                                                                                                                                                                                                                                                                        |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Subscription.Filter.Scope`      | **Type**: enum<br><br>**Description:** Events can be filtered by Stream or Record scopes using either regular expressions, JsonPath expressions, or prefixes. The expression is first checked as a regex, then as JsonPath, and if neither, it's used as a prefix for filtering.<br><br>**Accepted Values:**<br>- `Unspecified`<br>- `Stream`<br>- `Record`.<br><br>**Default**: `Unspecified` |
| `Subscription.Filter.Expression` | **Type**: string<br><br>**Description:** A regex, JsonPath expression or prefix to filter records.<br><br>**Default**: `""`                                                                                                                                                                                                                                                                            |
| `Subscription.InitialPosition`   | **Type**: enum<br><br>**Description:** Where to start consuming events from.<br><br>**Accepted Values:**<br>- `Latest`<br>- `Earliest`.<br><br>**Default**: `Latest`                                                                                                                                                                                                                           |

::: warning
JsonPath filters apply exclusively to events with the `application/json` content type. By default, if no filter is specified, the system will consume from the `$all` stream, excluding system events.
:::

**Example usage of stream ID prefix filter**

```json
{
  "InstanceTypeName": "EventStore.Connectors.Http.HttpSink",
  "Subscription:Filter:Scope": "Stream",
  "Subscription:Filter:Expression": "prefix_"
}
```

**Example usage of record regex filter**

```json
{
  "InstanceTypeName": "EventStore.Connectors.Http.HttpSink",
  "Subscription:Filter:Scope": "Record",
  "Subscription:Filter:Expression": "^eventType.*"
}
```

**Example usage of JsonPath filter**

The following objects are accessible to the filter:

- System metadata via $, for example $.eventType or $.stream
- Event data via $.data, for example $.data.name or $.data.age
- Event metadata via $.metadata, for example $.metadata.user or $.metadata.correlationId

```json
{
  "InstanceTypeName": "EventStore.Connectors.Http.HttpSink",
  "Url": "https://enf4k0vsrz29w.x.pipedream.net/",
  "Subscription:Filter:Scope": "Record",
  "Subscription:Filter:Expression": "$[?($.data.testField=='testValue')]"
}
```

::: tip
Learn more about [JsonPath](https://www.rfc-editor.org/rfc/rfc9535.html). You
can also use the [JsonPath Playground](https://json-everything.net/json-path/)
to test your expressions.
:::


## Transformation configuration

| Name                             | Details                                                                                                                                                                       |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Transformer.Enabled`            | **Type**: bool<br><br>**Description:** Enables or disables the event transformer.<br><br>**Default**: `false`                                                                 |
| `Transformer.Function`           | **Type**: string<br><br>**Description:** Base64 encoded JavaScript function for transforming events.<br><br>**Default**: `""`                                                 |
| `Transformer.FunctionName`       | **Type**: string<br><br>**Description:** Name of the transformation function.There should be a function present in the script with this name.<br><br>**Default**: `transform` |
| `Transformer.ExecutionTimeoutMs` | **Type**: int<br><br>**Description:** Maximum time in milliseconds the transform function is allowed to execute.<br><br>**Default**: `3000`                                   |

**Example configuration for transformation**

```json
{
  "Transformer:Enabled": "true",
  "Transformer:FunctionName": "transform",
  "Transformer:Function": "ZnVuY3Rpb24gdHJhbnNmb3JtKHRyYW5zZm9ybVJlY29yZCkgewogIGxldCB7IFZhbHVlLCBIZWFkZXJzIH0gPSB0cmFuc2Zvcm1SZWNvcmQ7CiAgcmV0dXJuIHsKICAgIC4uLnRyYW5zZm9ybVJlY29yZCwKICAgIFZhbHVlOiB7CiAgICAgIE5hbWU6IFZhbHVlLkZpcnN0TmFtZSArICcgJyArIFZhbHVlLkxhc3ROYW1lCiAgICB9CiAgfTsKfQo="
}
```

For an example of how to use transformations, refer to the [Quick start](./quickstart.md#applying-transformations) section.
