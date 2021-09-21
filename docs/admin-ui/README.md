# Admin UI

The EventStoreDB Admin UI is available at _SERVER_IP:2113_ and helps you interact with and manage a cluster in a visual way. This guide explains the tabs of the interface and what they do.

## Dashboard

::: card

![Web admin interface dashboard](../images/wai-dashboard.png)

:::

The _Dashboard_ tab shows an overview of active queues with associated statistics in the top half. The _+_ icon indicates a queue group, click it to reveal the queues that are part of the group.

The second half of the tab shows active connections to EventStoreDB and information about them.

Click the _Snapshot_ button in the top right to output a snapshot of all queue statistics at the time you clicked the button.

## Stream browser

::: card

![Web admin interface stream browser tab](../images/wai-stream-browser.png)

:::

The _Stream Browser_ tab gives an overview of recently created and changed streams, clicking on an individual stream shows details about the individual stream.

### Event stream

::: card

![Web admin interface stream details](../images/wai-stream-details.png)

:::

Each individual stream shows pages of the events in a stream with an overview of the event. Click the _Name_ to see the EventId, and _JSON_ to the see the event data. The buttons above change depending on what you are viewing in the interface. The _Back_ button takes you to the parent screen.

The buttons on the top right when you are viewing an event stream are:

- _Pause_: Stop showing events arriving into this stream.
- _Resume_: Resume showing events arriving into this stream.
- _Edit ACL_: Edit [the access control lists](../security/acl.md) for a stream.
- _Add Event_: [Add a new event](/clients/http-api/v5/introduction/appending-events.md) to the stream.
- _Delete_: [Delete a stream](/clients/http-api/v5/introduction/deleting-a-stream.md) to the stream.
- _Query_:

The buttons on the left above the events when you are viewing an event stream are:

- _self_: Takes you to the overview of the stream.
- _first_: Takes you to the first page of events in a stream.
- _previous_: Takes you to the previous page of events in a stream.
- _metadata_: Shows the metadata of a stream.
  - On the metadata screen, click _Add New Like This_ to add a new event to the stream.

## Projections

::: card

![Web admin interface projections tab](../images/wai-projections.png)

:::

The _Projections_ tab shows system and user created projections defined in EventStoreDB, the buttons above the list do the following:

- _Disable All_: Disable all running projections.
- _Enable All_: Enable all stopped projections.
- _Include Queries_: Toggle displaying queries in the Projections table.
- _New Projection_: [Create a user-defined projection](../projections/user-defined-projections.md) with the Admin UI.

Clicking an individual projection shows further details.

::: card

![Web admin interface projection details](../images/wai-projection-details.jpg)

:::

On the left is the projection definition, and on the right, the stats, results and state of the projection. The buttons above the details do the following:

- _Start_: Start a stopped projection.
- _Stop_: Stop a running projection.
- _Edit_: Edit the projection definition.
- _Config_: [Set configuration options](../projections/projections-config.md) for a projection.
- _Debug_: Opens [the debugging interface](../projections/debugging.md) to debug what effect a projection is having on events.
- _Delete_: Delete a projection.
- _Reset_: Reset a projection.
- _Back_: Returns you to the parent screen.

## Query

The _Query_ tab a code editor field where you can create transient and short-lived projections for quick analysis of your event streams.

::: card

![Web admin interface query details](../images/wai-query-details.png)

:::

## Persistent subscriptions

The _Persistent Subscriptions_ tab shows an overview of [persistent subscriptions](/clients/http-api/v5/persistent/README.md) configured on streams. The button above the list do the following:

- _New Subscription_: Create a new [subscriptions](/clients/http-api/v5/introduction/reading-subscribing-events.md)

Clicking the _+_ icon next to a stream name reveals the subscription name and more buttons. The _Back_ button takes you to the parent screen. :

- _Edit_: Edit the subscription.
- _Delete_: Delete the subscription.
- _Detail_: Shows the subscription configuration options.
- _Replay Parked Messages_: Replay events in subscription to return state.

## Admin

::: card

![Web admin interface projections tab](../images/wai-admin.png)

:::

The _Admin_ tab shows sub systems enabled (currently only [projections](../projections/)) on EventStoreDB and [scavenges](../operations/scavenge.md) run. You can start a new scavenge operation by clicking the _Scavenge_ button, and shut EventStoreDB down by clicking the _Shutdown Server_ button.

## Users

::: card

![Web admin interface projections tab](../images/wai-users.png)

:::

The _Users_ tab shows [the users defined in EventStoreDB](../security/acl.md), clicking an individual user shows a JSON representation of that users details.

## Log out

Logs you out of the Admin UI interface.
