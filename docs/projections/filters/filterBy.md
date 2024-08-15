# filterBy

Causes projection results to be `null` for any `state` that returns a `false` value from the given predicate.

## Syntax

```js
.filterBy(predicate)
```

### Usage

```js
// Only retrieve resulrs for significant accounts
.filterBy(function (state) {
  return state.accountBalance > 1000
})
```

### Arguments

- **handlers _(Object)_**

An object whose properties contain functions which process the incoming events.

### Chains to

    [transformBy](../transformations/transformBy.md)
    [filterBy](./filterBy.md)
    [outputTo](../outputs/outputTo.md)
    outputState(../outputs/outputState.md)

## Examples

### Find all deposits above 1000 dollars

```js

```
