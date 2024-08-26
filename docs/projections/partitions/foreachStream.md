# foreachStream

Run a projection pipeline (with separate state) for each input stream.

## Syntax

```js
.foreachStream()
```

### Usage

```js
fromCategory("account")
  .foreachStream()
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

### Chains to

[when](../filters/when.md)
