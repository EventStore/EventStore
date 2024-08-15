# foreachStream

Run a projection pipeline (with separate state) for each input stream.

## Syntax

```js
.foreachStream(handlers)
```

### Usage

```js
fromStreams(["website-enquiries", "email-enquiries"]).foreachStream({
  // Count each type of event in the input streams
  $any: function (state, event) {
    return {
      ...state,
      // Increment the count on the depending on the type of the event
      [event.type]: (state[event.type] ?? 0) + 1,
    }
  },
})
```

### Arguments

**handlers _(Object)_**

| Handler        | Description                                                                               | Example                                                                  |
| -------------- | ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `{event-type}` | A handler for events of type `{event-type}`.                                              | `FundsDeposited: function(state, event) { }`                             |
| `$init`        | Provide the initial state for a projection.                                               | `$init: function() { return { orderTotal: 0 } }`                         |
| `$initShared`  | Provide the initialization for a projection which has both shared and partitioned states. | `$initShared: function() { return { totalRevenue: 0 } }`                 |
| `$any`         | A handler called for any event type which does not have an `{event-type}` handler.        | `$any: function(state, event) { return { count: state.count + 1 } }`     |
| `$deleted`     | Called upon the deletion of a stream. Can only be used with `foreachStream`.              | `$deleted: function(state) { return { ...state, streamDeleted: true } }` |

### Chains to

[partitionBy](../transformations/partitionBy.md)
[when](../filters/when.md)
[outputState](../transformations/outputState.md)
