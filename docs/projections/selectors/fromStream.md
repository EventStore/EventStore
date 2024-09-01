# fromStream

Selects events from a single stream.

## Syntax

```js
fromStream(streamId)
```

### Usage

```js
fromStream("customer-1")
```

### Arguments

**streamId _(string)_**

ID of the stream used as input.

### Chains from

None.

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../when.md)

[outputState](../outputs/outputState.md)

## Examples

### Count events in a stream

```js
fromStream("customer-1").when({
  $init: function () {
    return { count: 0 }
  },
  $any: function (state, event) {
    return { count: state.count + 1 }
  },
})
```
