---
Title: 'Connector Settings'
Order: 3
---

# Connector Settings

All connectors share a common set of configuration options that can be used to
customize their behavior.

## Sink Options

Below, you will find a comprehensive list of configuration settings that you can
use to define the connection parameters and other necessary details for your
sink connector.

::: tip
Individual connectors also include their own specific settings. To view them, go to their individual pages.
:::


### Instance configuration

| Name               | Details                                                                                                                                                                                             |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `instanceTypeName` | _required_<br><br>**Type**: string<br><br>**Description:** The name of the instance type for the sink.<br><br>Refer to the sink's individual page for more details on the available instance types. |

### Subscription configuration

| Name                             | Details                                                                                                                                                                                                                                                                                                                                                                                |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `subscription:filter:scope`      | **Type**: enum<br><br>**Description:** Events can be filtered by Stream or Record scopes using either regular expressions, JsonPath expressions, or prefixes. The expression is first checked as a regex, then as JsonPath, and if neither, it's used as a prefix for filtering.<br><br>**Accepted Values:**<br>- `Unspecified`, `Stream`, `Record`.<br><br>**Default**: `Unspecified` |
| `subscription:filter:expression` | **Type**: string<br><br>**Description:** A regex or JsonPath expression to filter records.<br><br>**Default**: `""`                                                                                                                                                                                                                                                                    |
| `subscription:initialPosition`   | **Type**: enum<br><br>**Description:** Where to start consuming events from.<br><br>**Accepted Values:**<br>- `Latest`, `Earliest`.<br><br>**Default**: `Latest`                                                                                                                                                                                                                       |
| `subscription:startPosition`     | **Type**: ulong<br><br>**Description:** Explicit position in the log from which to start consuming records. If an enum value like `Latest` is used, it will automatically convert to the corresponding `ulong` value.<br><br>**Accepted Values:**<br>- Enum values: `Latest`, `Earliest`, `Unset`<br>- `ulong` values<br><br>**Default**: `Unset`                                      |

For details and examples on subscriptions, see [Subscriptions](./features#subscriptions).

### Transformation configuration

| Name                   | Details                                                                                                                                                                                                                      |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `transformer:enabled`  | **Type**: bool<br><br>**Description:** Enables or disables the event transformer.<br><br>**Default**: `false`                                                                                                                |
| `transformer:function` | _required (if enabled)_<br><br>**Type**: string<br><br>**Description:** Base64 encoded JavaScript function for transforming events. See [Transformations](./features#transformations) for examples.<br><br>**Default**: `""` |

For details and examples on transformations, see [Transformations](./features#transformations).

### Logging

| Name              | Details                                                                                        |
| ----------------- | ---------------------------------------------------------------------------------------------- |
| `logging:enabled` | **Type**: bool<br><br>**Description:** Enables or disables logging.<br><br>**Default**: `true` |
