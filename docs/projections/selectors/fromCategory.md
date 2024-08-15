# fromCategory

Selects events from the `$ce-{category}` stream created by the [`$by_category` system projection](../../projections.md#by-category).

::: warning
Note that the `fromCategory` selector relies on the `$by_category` system projection. If the `$by_category` projection is not running, `fromCategory` will not pick up new events added to streams within the specified category.
:::

## Syntax

```js
fromCategory(category)
```

### Usage

```js
// Selects events from the $ce-user stream
fromCategory("user")
```

### Arguments

**category _(string)_**

Name of the category.

### Chains to

[partitionBy](../transformations/partitionBy.md)

[when](../filters/when.md)

[foreachStream](../transformations/foreachStream.md)

[outputState](../outputs/outputState.md)

## Examples

### Count the number of deactivated users

```js
fromCategory("user").when({
  $init: function () {
    return { numDeactivatedUsers: 0 }
  },
  DeactivateUser: function (state, event) {
    return { numDeactivatedUsers: state.numDeactivatedUsers + 1 }
  },
  ReactivateUser: function (state, event) {
    return { numDeactivatedUsers: state.numDeactivatedUsers - 1 }
  },
})
```
