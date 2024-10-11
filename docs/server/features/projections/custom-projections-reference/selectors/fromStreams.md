# fromStreams

Selects events from multiple streams.

## Syntax

```js
fromStreams(streamIdArray)
```

### Usage

```js
fromStreams(["website-enquiries", "email-enquiries"])
```

### Arguments

**streamIds _(Array<string>)_**

IDs of the streams used as input.

### Chains from

None.

### Chains to

[partitionBy](../partitions/partitionBy.md)

[when](../when.md)

[outputState](../outputs/outputState.md)

## Examples

### Take enquiry events from multiple sources and add them to the appropriate customer stream

```js
fromStreams(["website-enquiries", "email-enquiries"]).when({
  $any: function (state, event) {
    // Track the origin of the enquiry on the emitted event
    const enquiryOrigin =
      event.streamId === "website-enquiries" ? "Website" : "Email"

    const emittedEventData = {
      ...event.data,
      enquiryOrigin,
    }

    emit(
      `customer-${event.data.customerId}`,
      event.eventType,
      emittedEventData,
      event.metadata
    )
  },
})
```
