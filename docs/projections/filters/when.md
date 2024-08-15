# when

Performs a fold operation across the events in the projection: each event is processed according to the specified handlers.

## Syntax

```js
.when(handlers)
```

### Usage

```js
fromStream("email-enquiries").when({
  $init: function () {
    // Initialise the state, which is passed to the other handlers
    return { numEventsProcessed: 0 }
  },
  $any: function (state, event) {
    // Code to return the updated state goes here
    return { numEventsProcessed: state.numEventsProcessed + 1 }
  },
})
```

### Arguments

- **handlers _(Object)_**

An object whose properties contain functions which process the incoming events.

| Handler        | Description                                                                               | Example                                                              |
| -------------- | ----------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `{event-type}` | A handler for events of type `{event-type}`.                                              | `FundsDeposited: function(state, event) { }`                         |
| `$init`        | Provide the initial state for a projection.                                               | `$init: function() { return { orderTotal: 0 } }`                     |
| `$initShared`  | Provide the initialization for a projection which has both shared and partitioned states. | `$initShared: function() { return { totalRevenue: 0 } }`             |
| `$any`         | A handler called for any event type which does not have an `{event-type}` handler.        | `$any: function(state, event) { return { count: state.count + 1 } }` |

### Chains to

[transformBy](../transformations/transformBy.md)

[filterBy](./filterBy.md)

[outputTo](../outputs/outputTo.md)

[outputState](../outputs/outputState.md)

## Examples

### Computing a bank account balance

```js
fromStream("account-d7fa4899-1e7e-41ae-9be3-075f52bbcc3c").when({
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
```
