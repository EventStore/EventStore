# Subscribe to changes

This guide shows you how to get started with EventStoreDB using the Atom publishing protocol as the primary interface. 

::: warning
The described is for development and evaluation of EventStoreDB. It does not describe a production setup. The HTTP examples use cURL, but you can read Atom feeds with a wide variety of applications and languages.
:::

## Subscribing to receive stream updates

A common operation is to subscribe to a stream and receive notifications for changes. As new events arrive, you continue following them.

### Subscription types

There are three types of subscription patterns, useful in different situations.

#### Volatile subscriptions

This subscription calls a given function for events appended after establishing the subscription. They are useful when you need notification of new events with minimal latency, but where it's not necessary to process every event.

For example, if a stream has 100 events in it when a subscriber connects, the subscriber can expect to see event number 101 onwards until the time the subscription is closed or dropped.

#### Catch-up subscriptions

This subscription specifies a starting point, in the form of an event number or transaction file position. You call the function for events from the starting point until the end of the stream, and then for subsequently appended events.

For example, if you specify a starting point of 50 when a stream has 100 events, the subscriber can expect to see events 51 through 100, and then any events are subsequently appended until you drop or close the subscription.

#### Persistent subscriptions

::: tip
Persistent subscriptions exist in version 3.2.0 and above of EventStoreDB.
:::

In contrast to volatile and Catch-up types persistent subscriptions are not dropped when connection is closed. Moreover, this subscription type supports the "[competing consumers](https://www.enterpriseintegrationpatterns.com/patterns/messaging/CompetingConsumers.html)" messaging pattern and are useful when you need to distribute messages to many workers. EventStoreDB saves the subscription state server-side and allows for at-least-once delivery guarantees across multiple consumers on the same stream. It is possible to have many groups of consumers compete on the same stream, with each group getting an at-least-once guarantee.

<!-- ## TODO: Need HTTP API examples for subscriptions with Atom -->


