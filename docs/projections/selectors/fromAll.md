# fromAll

Selects events from the [`$all` stream](../../streams.md#all).

## Syntax

```js
fromAll()
```

### Usage

```js
fromAll()
// ...chain to filters or transformations
```

### Arguments

None.

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../filters/when.md)

[foreachStream](../transformations/foreachStream.md)

[outputState](../outputs/outputState.md)

## Examples

### Combine events from multiple streams
