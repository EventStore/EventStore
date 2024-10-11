# partitionBy

Partitions the projection according to the specified partition function.

## Syntax

```js
.parititonBy(partitionFunction)
```

### Usage

```js
fromCategory("account")
  .partitionBy(function (event) {
    // Partition by event type
    return event.eventType
  })
  .when({
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

None.

### Chains from

[fromAll](../selectors/fromAll.md)

[fromCategory](../selectors/fromCategory.md)

### Chains to

[when](../filters/when.md)
