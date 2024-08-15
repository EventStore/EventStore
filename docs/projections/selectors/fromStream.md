# fromStream

Selects events from a single stream.

## Syntax

```js
fromStream(streamId)
```

### Usage

```js
fromStream("user-1")
```

### Arguments

**streamId _(string)_**

ID of the stream used as input.

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../filters/when.md)

[outputState](../outputs/outputState.md)

## Examples

### Linking to events from one stream to another

```js
fromStream("user-1").when({
  $any: function (state, event) {
    linkTo("all-users", event, {})
  },
})
```
