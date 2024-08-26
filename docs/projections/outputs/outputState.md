# outputState

Causes a stream called `$projections-{projection-name}-result` to be produced with the state as the event body.

If the projection is running in `Continuous` mode, the projection will create a Result event in the `$projections-{projection-name}-result` stream for each input event.

If the projection is running in `OneTime` mode, the projection will create a single Result event in the `$projections-{projection-name}-result` stream with the final state of the projection.

## Syntax

```js
outputState()
```

### Usage

```js
// Count the number of events of each type from the "account" category
fromCategory("account")
  .when({
    // Count each type of event in the input streams
    $any: function (state, event) {
      return {
        ...state,
        // Increment the count on the depending on the type of the event
        [event.eventType]: (state[event.eventType] ?? 0) + 1,
      }
    },
  })
  .outputState()
```

### Arguments

None.

### Chains from

[when](../filters/when.md)

[transformBy](../transformations/transformBy.md)

[filterBy](../outputs/filterBy.md)

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../filters/when.md)

[outputTo](../outputs/outputTo.md)
