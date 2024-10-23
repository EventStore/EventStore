---
title: 'Connector Settings'
order: 3
---

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

| Name                             | Details                                                                                                                                                                                                                                                                                                                                                                              |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `subscription:filter:scope`      | **Type**: enum<br><br>**Description:** Events can be filtered by Stream or Record scopes using either regular expressions, JsonPath expressions, or prefixes. The expression is first checked as a regex, then as JsonPath, and if neither, it's used as a prefix for filtering.<br><br>**Accepted Values:**<br>- `Stream`, `Record` or unspecified.<br><br>**Default**: Unspecified |
| `subscription:filter:expression` | **Type**: string<br><br>**Description:** A regex, JsonPath expression or prefix to filter records. If no filter is specified, the system will consume from the $all stream, excluding system events.<br><br>**Default**: `""`                                                                                                                                                        |
| `subscription:initialPosition`   | **Type**: enum<br><br>**Description:** The position in the message stream from which a consumer starts consuming messages when there is no prior checkpoint.<br><br>**Accepted Values:**<br>- `Latest`, `Earliest`.<br><br>**Default**: `Latest`                                                                                                                                     |
| `subscription:startPosition`     | **Type**: ulong<br><br>**Description:** The precise position in the log from which to start consuming records.<br><br>- **Default**: ""                                                                                                                                                                                                                                              |

For details and examples on subscriptions, see [Filters](./features.md#filters).

### Transformation configuration

| Name                   | Details                                                                                                                                                                                                                         |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `transformer:enabled`  | **Type**: bool<br><br>**Description:** Enables or disables the event transformer.<br><br>**Default**: `false`                                                                                                                   |
| `transformer:function` | _required (if enabled)_<br><br>**Type**: string<br><br>**Description:** Base64 encoded JavaScript function for transforming events. See [Transformations](./features.md#transformations) for examples.<br><br>**Default**: `""` |

For details and examples on transformations, see [Transformations](./features.md#transformations).

### Resilience configuration

| Name                                       | Details                                                                                                                                                                |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `resilience:enabled`                       | **Type**: bool<br><br>**Description:** Enables or disables resilience.<br><br>**Default**: `true`                                                                      |
| `resilience:firstDelayBound:upperLimitMs`  | **Type**: int<br><br>**Description:** The upper limit for the first delay bound in milliseconds.<br><br>**Default**: `60000` (1 minute)                                |
| `resilience:firstDelayBound:delayMs`       | **Type**: int<br><br>**Description:** The delay for the first delay bound in milliseconds.<br><br>**Default**: `5000` (5 seconds)                                      |
| `resilience:secondDelayBound:upperLimitMs` | **Type**: int<br><br>**Description:** The upper limit for the second delay bound in milliseconds.<br><br>**Default**: `3600000` (1 hour)                               |
| `resilience:secondDelayBound:delayMs`      | **Type**: int<br><br>**Description:** The delay for the second delay bound in milliseconds.<br><br>**Default**: `600000` (10 minutes)                                  |
| `resilience:thirdDelayBound:upperLimitMs`  | **Type**: int<br><br>**Description:** The upper limit for the third delay bound in milliseconds. A value of `-1` indicates forever.<br><br>**Default**: `-1` (forever) |
| `resilience:thirdDelayBound:delayMs`       | **Type**: int<br><br>**Description:** The delay for the third delay bound in milliseconds.<br><br>**Default**: `3600000` (1 hour)                                      |

For details on resilience, see [Resilience](./features.md#resilience).

### Logging configuration

| Name              | Details                                                                                        |
| ----------------- | ---------------------------------------------------------------------------------------------- |
| `logging:enabled` | **Type**: bool<br><br>**Description:** Enables or disables logging.<br><br>**Default**: `true` |
