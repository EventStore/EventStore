# outputTo

Outputs the projection state to the specified stream.

In the case of partitioned state, each state can be output to a different stream depending on the supplied template string.

If the projection is running in `Continuous` mode, the projection will create a Result event in the specified stream for each input event.

If the projection is running in `OneTime` mode, the projection will create a single Result event in the specified stream with the final state of the projection.

## Syntax

```js
.outputTo(streamId)

.outputTo(sharedStateStreamId, partitionedStateStreamId)
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
  // Output the state to the stream "account-event-statistics"
  .outputTo("account-event-statistics")
```

### Arguments

None.

### Chains from

[when](../filters/when.md)

[transformBy](../transformations/transformBy.md)

[filterBy](../transformations/filterBy.md)

### Chains to

None.

## Examples

### Outputting partitioned state to separate streams

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
  // Output the state to the stream "account-event-statistics"
  .outputTo("account-event-statistics")
```
