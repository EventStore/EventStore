# User defined projections

::: warning WIP
This page is work in progress.
:::

<!-- TODO: Again refactor to shopping cart? -->

<!-- TODO: Use cases and performance implications. -->

<!-- ## Overview -->

You create user defined projections in JavaScript. For example, the `my_demo_projection_result` projection below counts the number of `myEventType` events from the `account-1` stream. It then uses the `transformBy` function to change the final state:

```javascript
options({
	resultStreamName: "my_demo_projection_result",
	$includeLinks: false,
	reorderEvents: false,
	processingLag: 0
})

fromStream('account-1')
.when({
	$init:function(){
		return {
			count: 0
		}
	},
	myEventType: function(state, event){
		state.count += 1;
	}
})
.transformBy(function(state){
	return { Total: state.count }
})
.outputState()
```

<!-- TODO: Show example output, see above comment -->

## User defined projections API

### Options

| Name | Description | Notes
|:---- |:----------- |:-----
`resultStreamName` | Overrides the default resulting stream name for the `outputState()` transformation, which is `$projections-{projection-name}-result`. | |
| `$includeLinks` |Configures the projection to include/exclude link to events. | Default: `false` |
| `processingLag` | When `reorderEvents` is enabled, this value is used to compare the total milliseconds between the first and last events in the buffer and if the value is equal or greater, the events in the buffer are processed. The buffer is an ordered list of events. | Default: `500ms`. Only valid for `fromStreams()` selector |
| `reorderEvents` | Process events by storing a buffer of events ordered by their prepare position | Default: `false`. Only valid for `fromStreams()` selector |

### Selectors

| Selector | Description | Notes
|:-------- |:----------- |:----- 
| `fromAll()` | Selects events from the `$all` stream. | **Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`foreachStream`</li><li>`outputState`</li></ul> |
| `fromCategory({category})` | Selects events from the `$ce-{category}` stream. | **Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`foreachStream`</li><li>`outputState`</li></ul> |
| `fromStream({streamId})` | Selects events from the `streamId` stream. | **Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`outputState`</li></ul> |
| `fromStreams(streams[])` | Selects events from the streams supplied. | **Provides**<ul><li>`partitionBy`</li><li>`when`</li><li>`outputState`</li></ul> |

### Filters/Transformations

| Filter/Partition | Description | Notes |
|:---------------- |:----------- |:----- |
|`when(handlers)` | Allows only the given events of a particular to pass through the projection. | **Provides** <ul><li>`$defines_state_transform` </li><li>`transformBy`</li><li>`filterBy`</li><li>`outputTo`</li><li>`outputState`</li></ul> |
| `foreachStream()` | Partitions the state for each of the streams provided. | **Provides** <ul><li>`when`</li></ul> |
| `outputState()` | If the projection maintains state, setting this option produces a stream called `$projections-{projection-name}-result` with the state as the event body. | **Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputTo`</li></ul> |
| `partitionBy(function(event))` | Partitions a projection by the partition returned from the handler. | **Provides** <ul><li>`when`</li></ul> |
| `transformBy(function(state))` | Provides the ability to transform the state of a projection by the provided handler. | **Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputState`</li><li>`outputTo`</li></ul> |
| `filterBy(function(state))` | Causes projection results to be `null` for any `state` that returns a `false` value from the given predicate. | **Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputState`</li><li>`outputTo`</li></ul> |                             

### Handlers

Each handler is provided with the current state of the projection as well as the event that triggered the handler. The event provided through the handler contains the following properties.

- `isJson`: true/false
- `data`: {}
- `body`: {}
- `bodyRaw`: string
- `sequenceNumber`: integer
- `metadataRaw`: {}
- `linkMetadataRaw`: string
- `partition`: string
- `eventType`: string

| Handler | Description | Notes |
|:------- |:----------- |:----- |
| `{event-type}` | When using `fromAll()` and 2 or more event type handlers are specified and the `$by_event_type` projection is enabled and running, the projection starts as a `fromStreams($et-event-type-foo, $et-event-type-bar)` until the projection has caught up and moves to reading from the transaction log (i.e. from `$all`). | |
| `$init` | Provide the initialization for a projection. | Commonly used to setup the initial state for a projection. |
| `$initShared` | Provide the initialization for a projection where the projection is possibly partitioned. | |
| `$any` | Event type pattern match that match any event type. | Commonly used when the user is interested in any event type from the selector. |
| `$deleted` | Called upon the deletion of a stream. | Can only be used with `foreachStream` |

### Functions

| Handler | Description |
|:------- |:----------- |
| `emit(streamId, eventType, eventBody, metadata)` | Appends an event to the designated stream |
| `linkTo(streamId, event, metadata)` | Writes a link to event to the designated stream |
