# outputState

If the projection maintains state, setting this option produces a stream called `$projections-{projection-name}-result` with the state as the event body.

## Syntax

```js
.outputState()
```

### Usage

```js
fromStreams(["website-enquiries", "email-enquiries"])
  .foreachStream({
    // Count each type of event in the input streams
    $any: function (state, event) {
      return {
        ...state,
        // Increment the count on the depending on the type of the event
        [event.type]: (state[event.type] ?? 0) + 1,
      }
    },
  })
  .outputState()
```

::: warning
`outputState` will cause the **final** state of the projection to be appended to `$projections-{projection-name}-result`, once the projection has finished running. `outputState` does not use the state at the time `outputState` was called: it uses the final state of the projection. To emit an intermediate state, use [`emit`](../emitters/emit)
:::

### Arguments

None.

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../filters/when.md)

[outputTo](../outputs/outputTo.md)
