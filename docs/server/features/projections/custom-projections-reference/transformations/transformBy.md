# transformBy

## Syntax

```js
.transformBy(transformFn)
```

### Usage

```js
// Only output results for significant accounts
.transformBy(function (state) {
  return state.accountBalance >= 1000 ? state : null
})
```

:::info
Note that a call to `transformBy` will implicitly call [`outputState`](../outputs/outputState.md): any projection with a `transformBy` call will output results. If necessary output stream name can be set using [`outputTo`](../outputs/outputTo.md).
:::

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

[filterBy](./transformations/filterBy.md)

[outputTo](../outputs/outputTo.md)

[outputState](../outputs/outputState.md)

## Examples

### Find all accounts with a balance greater than 1000

```js
fromStream("account-123")
  .when({
    $init: function () {
      return { accountBalance: 0 }
    },
    // When an event of type 'FundsDeposited' is processed, add to the accountBalance
    FundsDeposited: function (state, event) {
      return { accountBalance: state.accountBalance + event.data.amount }
    },
    // When an event of type 'FundsWithdrawn' is processed, subtract from the accountBalance
    FundsWithdrawn: function (state, event) {
      return { accountBalance: state.accountBalance - event.data.amount }
    },
  })
  // Output the projection state to $projections-{projection-name}-result.
  // If the account balance is less than 1000 then the output event data will be "null".
  .transformBy(function (state) {
    return state.accountBalance >= 1000
  })
```
