---
title: emit
order: 1
---

# emit

Appends an event to a stream. Can only be used within the handler of [`when`](../when.md).

## Syntax

```js
emit(streamId, eventType, eventBody, metadata)
```

### Usage

Within a handler function inside a [`when`](../when.md) call, `emit` can be used as below:

```js
emit("customer-123", "AccountDeactivated", { reason: "..." }, {})
```

:::warning
If a projection emits to a stream, then the target stream becomes **managed** by the projection. This means only the projection can append events to the stream. You should not append events to projection-managed streams using any other mechanism: if other events are appended to the managed stream then the projection will fail.

In the example above, `customer-123` is a stream managed by the projection. There should not be any clients or other projections writing events to `customer-123`.
:::

### Arguments

- **streamId _(string)_**

Specifies the stream to which events will be emitted.

- **eventType _(string)_**

Type of the emitted event.

- **eventBody _(object)_**

A JavaScript object representing the JSON body of the emitted event. The event object can be obtained from the parameters of the handlers supplied to [`when`](../when.md).

- **metadata _(object)_**

A JavaScript object representing the JSON metadata of the emitted event.

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
