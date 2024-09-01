# filterBy

Causes projection results to be `null` for any `state` that returns a `false` value from the given predicate.

## Syntax

```js
.filterBy(predicate)
```

### Usage

```js
// Only retrieve results for significant accounts
.filterBy(function (state) {
  return state.accountBalance > 1000
})
```

### Arguments

- **predicate _(Function (State => boolean))_**

A function which takes the current state and returns a boolean.

### Chains from

[fromAll](../selectors/fromAll.md)

[fromCategory](../selectors/fromCategory.md)

[fromStream](../selectors/fromStream.md)

[fromStreams](../selectors/fromStreams.md)

[foreachStream](../transformations/foreachStream.md)

[partitionBy](../transformations/partitionBy.md)

### Chains to

[transformBy](../transformations/transformBy.md)

[filterBy](./filterBy.md)

[outputTo](../outputs/outputTo.md)

[outputState](../outputs/outputState.md)

## Examples

### Find all accounts with a balance greater than 1000

```js

```
