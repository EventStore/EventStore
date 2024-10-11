# linkTo

Appends a LinkTo event to a stream. Can only be used within the handler of [`when`](../filters/when.md).

## Syntax

```js
linkTo(streamId, event, linkToEventMetadata)
```

### Usage

Within a handler function inside a [`when`](../filters/when.md) call, `linkTo` can be used as below:

```js
fromStreams(["website-enquiries", "email-enquiries"]).when({
  $any: function (state, event) {
    linkTo(`customer-${event.data.customerId}`, event, {
      linkOrigin: "enquiry-link-projection",
    })
  },
})
```

This projection will link events from the `website-enquiries` and `email-enquiries` streams to the streams for each customer.

### Arguments

- **streamId _(string)_**

Specifies the stream to which the LinkTo event will be emitted.

- **event _(Event)_**

Event which will be linked.

- **linkToEventMetadata _(object)_**

A JavaScript object representing the JSON metadata of the LinkTo event.
