---
title: Projections
---

## Introduction

Projections is an EventStoreDB subsystem that lets you append new events or link existing events to streams in
a reactive manner.

Projections are good at solving one specific query type, a category known as 'temporal correlation queries'.
This query type is common in business systems and few can execute these queries well.

::: tip 
Projections require the event body to be in JSON.
:::

### Business case examples

For example. You are looking for how many Twitter users said "happy" within 5 minutes of the word "foo coffee
shop" and within 2 minutes of saying "london".

This is the type of query that projections can solve. Let's try a more complex business problem.

As a medical research doctor you want to find people diagnosed with pancreatic cancer within the last year.
During their treatment a patient should not have had any proxies for a heart condition such as taking aspirin
every morning. Within three weeks of their diagnosis they should have been put on treatment X. Within one
month after starting the treatment they should have failed with a lab result that looks like L1. Within
another six weeks they should have been put on treatment Y, and within four weeks failed that treatment with a
lab result that looks like L2.

You can use projections in nearly all examples of near real-time complex event processing. There are a large
number of problems that fit into this category from monitoring of temperature sensors, to reacting to changes
in the stock market.

It's important to remember the types of problems that projections help to solve. Many problems are not a good
fit for projections and are better served by hosting another read model populated by a catchup subscription.

### Continuous querying

Projections support the concept of continuous queries. When running a projection you can choose whether the
query should run and give you all results present, or whether the query should continue running into the
future finding new results as they happen and updating its result set.

In the medical example above the doctor could leave the query running to be notified of any new patients that
meet the criteria. The output of all queries is a stream, you can listen to this stream like any other stream.

### Types of projections

There are two types of projections in EventStoreDB:

- [Built in (system) projections](#system-projections)
- [User-defined JavaScript projections](#user-defined-projections) which you create via the API or the admin
  UI

### Performance impact

Keep in mind that all projections emit events as a reaction to events that they process. We call this effect _
write amplification_ because emitting new events or link events creates additional load on the server IO.

Some system projections emit link events to their streams for each event appended to the database. These
projections are By Category, By Event Type and By Correlation Id. If all those three projections are enabled
and started, adding one event to the database will, in fact, produce three additional events and, therefore,
quadruples the number of write operations.

System projections `$streams` and `$stream-by-category` produce new events too, either per each new stream or
per new stream category. If your system has a lot of small streams, the `$streams` system projection would
also amplify writes significantly.

Custom projections create the most significant write amplification since they produce new events or link
events, which in turn get processed by system projections.

Projections only run on a leader node of the cluster due to consistency concerns. It creates more CPU and IO
load on the leader node compared to follower nodes.

### Limitations

Streams where projections emit events cannot be used to append events from applications. When this happens,
the projection will detect events not produced by the projection itself and it will break.

The reason projections exclusively own their streams is that otherwise they would lose all predictability. The
projection would no longer have any idea what should be in that stream. For example, when a projection starts
up from a checkpoint, it first goes through all the events after that checkpoint and checks them against the
emitted stream. By doing this, the projection can understand if it up to the last event and can continue from
where it left off. On top of that, the projection can verify that everything is in order, no events missing,
etc. If anyone can append to the emitted streams, then the projection would have no idea where it got to last
in terms of processing. Therefore, it can no longer trust that the projection itself emitted that event or if
something else did.

## System projections

EventStoreDB ships with five built in projections:

- [By Category](#by-category) (`$by_category`)
- [By Event Type](#by-event-type) (`$by_event_type`)
- [By Correlation ID](#by-correlation-id) (`$by_correlation_id`)
- [Stream by Category](#stream-by-category) (`$stream_by_category`)
- [Streams](#streams-projection) (`$streams`)

### Enabling system projections

When you start EventStoreDB from a fresh database, these projections are present but disabled and querying
their statuses returns `Stopped`. You can enable a projection by issuing a request which switches the status
of the projection from `Stopped` to `Running`.

```bash:no-line-numbers
curl -i -X POST "http://{event-store-ip}:{ext-http-port}/projection/{projection-name}/command/enable" -H "accept:application/json" -H "Content-Length:0" -u admin:changeit
```

### By category

The `$by_category` (_http://127.0.0.1:2113/projection/$by_category_) projection links existing events from
streams to a new stream with a `$ce-` prefix (a category) by splitting a stream `id` by a configurable
separator.

```text:no-line-numbers
first
-
```

You can configure the separator, as well as where to split the stream `id`. You can edit the projection and
provide your own values if the defaults don't fit your particular scenario.

The first parameter specifies how the separator is used, and the possible values for that parameter is `first`
or `last`. The second parameter is the separator, and can be any character.

For example, if the body of the projection is `first` and `-`, for a stream id
of `account-9E763770-0A8D-456D-AF23-410ADBC88249`, the stream name the projection creates is `$ce-account`.

If the body of the projection is `last` and `-`, for a stream id of `shopping-cart-1`, the stream name the
projection creates is `$ce-shopping-cart`.

::: warning 
You can change the projection setting at any time, so it can be quite dangerous. Consider all
possible event consumers of the category stream that expect it to be in the format that is already there.
Changing the setting might break all of them.
:::

The use case of this project is subscribing to all events within a category.

### By event type

The `$by_event_type` (_http://127.0.0.1:2113/projection/$by_event_type_) projection links existing events from
streams to a new stream with a stream id in the format `$et-{event-type}`.

For example, if you append an event with the `EventType` field set to `PaymentProcessed`, no matter in what
stream you appended this event, you get a link event in the `$et-PaymentProcessed` stream.

You cannot configure this projection.

### By correlation ID

The `$by_correlation_id` (_http://127.0.0.1:2113/projection/$by_correlation_id_) projection links existing
events from projections to a new stream with a stream id in the format `$bc-<correlation id>`.

The projection takes one parameter, a JSON string as a projection source:

```json:no-line-numbers
{
  "correlationIdProperty": "$myCorrelationId"
}
```

### Stream by category

The `$stream_by_category` (_http://127.0.0.1:2113/projection/$by_category_) projection links existing events
from streams to a new stream with a `$category` prefix by splitting a stream `id` by a configurable separator.

```text:no-line-numbers
first
-
```

By default, the `$stream_by_category` projection links existing events from a stream id with a name such
as `account-1` to a stream called `$category-account`. You can configure the separator as well as where to
split the stream `id`. You can edit the projection and provide your own values if the defaults don't fit your
particular scenario.

The first parameter specifies how the separator is used, and the possible values for that parameter is `first`
or `last`. The second parameter is the separator, and can be any character.

For example, if the body of the projection is `first` and `-`, for a stream id of `account-1`, the stream name
the projection creates is `$category-account`, and the `account-1` stream is linked to it. Future streams
prefixed with `account-` are likewise linked to the newly created `$category-account` stream.

If the body of the projection is last and `-`, for a stream id of `shopping-cart-1`, the stream name the
projection creates is `$category-shopping-cart`, and the `shopping-cart-1` stream is linked to it. Future
streams whose left-side split by the _last_ `-` is `shopping-cart`, are likewise linked to the newly
created `$category-shopping-cart` stream.

The use case of this projection is subscribing to all stream instances of a category.

### Streams projection

The `$streams` (_http://127.0.0.1:2113/projection/$streams_) projection links existing events from streams to
a stream named `$streams`

You cannot configure this projection.

## User defined projections

::: warning 
WIP This section is work in progress.
:::

<!-- TODO: Again refactor to shopping cart? -->

<!-- TODO: Use cases and performance implications. -->

<!-- ## Overview -->

You create user defined projections in JavaScript. For example, the `my_demo_projection_result` projection
below counts the number of `myEventType` events from the `account-1` stream. It then uses the `transformBy`
function to change the final state:

```javascript
options({
    resultStreamName: "my_demo_projection_result",
    $includeLinks:    false,
    reorderEvents:    false,
    processingLag:    0
})

fromStream('account-1')
    .when({
        $init: function () {
            return {
                count: 0
            }
        },
        myEventType: function (state, event) {
            state.count += 1;
        }
    })
    .transformBy(function (state) {
        return {Total: state.count}
    })
    .outputState()
```

<!-- TODO: Show example output, see above comment -->

### User defined projections API

#### Options

| Name               | Description                                                                                                                                                                                                                                                  | Notes                                                     |
|:-------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:----------------------------------------------------------|
| `resultStreamName` | Overrides the default resulting stream name for the `outputState()` transformation, which is `$projections-{projection-name}-result`.                                                                                                                        |                                                           |
| `$includeLinks`    | Configures the projection to include/exclude link to events.                                                                                                                                                                                                 | Default: `false`                                          |
| `processingLag`    | When `reorderEvents` is enabled, this value is used to compare the total milliseconds between the first and last events in the buffer and if the value is equal or greater, the events in the buffer are processed. The buffer is an ordered list of events. | Default: `500ms`. Only valid for `fromStreams()` selector |
| `reorderEvents`    | Process events by storing a buffer of events ordered by their prepare position                                                                                                                                                                               | Default: `false`. Only valid for `fromStreams()` selector |

#### Selectors

| Selector                                                                                                | Description                                      | Notes |
|:--------------------------------------------------------------------------------------------------------|:-------------------------------------------------|:------|
| `fromAll()`                                                                                             | Selects events from the `$all` stream.           | **    |
| Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`foreachStream`</li><li>`outputState`</li></ul> |                                                  |       |
| `fromCategory({category})`                                                                              | Selects events from the `$ce-{category}` stream. | **    |
| Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`foreachStream`</li><li>`outputState`</li></ul> |                                                  |       |
| `fromStream({streamId})`                                                                                | Selects events from the `streamId` stream.       | **    |
| Provides** <ul><li>`partitionBy`</li><li>`when`</li><li>`outputState`</li></ul>                         |                                                  |       |
| `fromStreams(streams[])`                                                                                | Selects events from the streams supplied.        | **    |
| Provides**<ul><li>`partitionBy`</li><li>`when`</li><li>`outputState`</li></ul>                          |                                                  |       |

#### Filters and transformations

| Filter/Partition                                                                                                                           | Description                                                                                                                                               | Notes |
|:-------------------------------------------------------------------------------------------------------------------------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------|:------|
| `when(handlers)`                                                                                                                           | Allows only the given events of a particular to pass through the projection.                                                                              | **    |
| Provides** <ul><li>`$defines_state_transform` </li><li>`transformBy`</li><li>`filterBy`</li><li>`outputTo`</li><li>`outputState`</li></ul> |                                                                                                                                                           |       |
| `foreachStream()`                                                                                                                          | Partitions the state for each of the streams provided.                                                                                                    | **    |
| Provides** <ul><li>`when`</li></ul>                                                                                                        |                                                                                                                                                           |       |
| `outputState()`                                                                                                                            | If the projection maintains state, setting this option produces a stream called `$projections-{projection-name}-result` with the state as the event body. | **    |
| Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputTo`</li></ul>                                                           |                                                                                                                                                           |       |
| `partitionBy(function(event))`                                                                                                             | Partitions a projection by the partition returned from the handler.                                                                                       | **    |
| Provides** <ul><li>`when`</li></ul>                                                                                                        |                                                                                                                                                           |       |
| `transformBy(function(state))`                                                                                                             | Provides the ability to transform the state of a projection by the provided handler.                                                                      | **    |
| Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputState`</li><li>`outputTo`</li></ul>                                     |                                                                                                                                                           |       |
| `filterBy(function(state))`                                                                                                                | Causes projection results to be `null` for any `state` that returns a `false` value from the given predicate.                                             | **    |
| Provides** <ul><li>`transformBy`</li><li>`filterBy`</li><li>`outputState`</li><li>`outputTo`</li></ul>                                     |                                                                                                                                                           |       |

#### Handlers

Each handler is provided with the current state of the projection as well as the event that triggered the
handler. The event provided through the handler contains the following properties.

- `isJson`: true/false
- `data`: {}
- `body`: {}
- `bodyRaw`: string
- `sequenceNumber`: integer
- `metadataRaw`: {}
- `linkMetadataRaw`: string
- `partition`: string
- `eventType`: string

| Handler        | Description                                                                                                                                                                                                                                                                                                              | Notes                                                                          |
|:---------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:-------------------------------------------------------------------------------|
| `{event-type}` | When using `fromAll()` and 2 or more event type handlers are specified and the `$by_event_type` projection is enabled and running, the projection starts as a `fromStreams($et-event-type-foo, $et-event-type-bar)` until the projection has caught up and moves to reading from the transaction log (i.e. from `$all`). |                                                                                |
| `$init`        | Provide the initialization for a projection.                                                                                                                                                                                                                                                                             | Commonly used to setup the initial state for a projection.                     |
| `$initShared`  | Provide the initialization for a projection where the projection is possibly partitioned.                                                                                                                                                                                                                                |                                                                                |
| `$any`         | Event type pattern match that match any event type.                                                                                                                                                                                                                                                                      | Commonly used when the user is interested in any event type from the selector. |
| `$deleted`     | Called upon the deletion of a stream.                                                                                                                                                                                                                                                                                    | Can only be used with `foreachStream`                                          |

#### Functions

| Handler                                          | Description                                     |
|:-------------------------------------------------|:------------------------------------------------|
| `emit(streamId, eventType, eventBody, metadata)` | Appends an event to the designated stream       |
| `linkTo(streamId, event, metadata)`              | Writes a link to event to the designated stream |

## Configuring projections

By changing these settings, you can lessen the amount of pressure projections put on an EventStoreDB node or
improve projection performance. You can change these settings on a case-by-case basis, and monitor potential
improvements.

::: warning 
You can only change the configuration of a stopped projection.
:::

You change the configuration of a projection by setting the relevant key and value in a request, or when you
create a projection with the web admin interface.

::: card
![Web admin interface projections configuration screen](./images/wai-projection-config.jpg)
:::

<!-- TODO: Further explanation here -->

@[code{curl}](@samples/projections/create-projection.sh)

### Emit options

These options control how projections append events.

In busy systems, projections can put a lot of extra pressure on the master node. This is especially true for
EventStoreDB servers that also have persistent subscriptions running, which only the master node can process.
If you see a lot of commit timeouts and slow writes from your projections and other clients, then start with
these settings.

#### Emit enabled

The `emit` boolean setting determines whether a projection can emit events and any projection that
calls `emit()` or `linkTo()` requires it. If this option is not set and a projection attempts to emit events,
you see an error message like the following:

<!-- TODO: Is it emit or emitenabled? Or are .NET and HTTP different -->

```bash:no-line-numbers
'emit' is not allowed by the projection/configuration/mode
```

EventStoreDB disables this setting by default, and is usually set when you create the projection and if you
need the projection to emit events.

#### Track emitted streams

The `trackemittedstreams` boolean setting enables tracking of a projection's emitted streams. It only has an
affect if the projection has `EmitEnabled` enabled.

Tracking emitted streams enables you to delete a projection and all the streams that it has created. You
should only the setting if you intend to delete a projection and create new ones that project to the same
stream.

::: warning 
By default, EventStoreDB disables the `trackemittedstreams` setting for projections. When enabled,
an event appended records the stream name (in `$projections-{projection_name}-emittedstreams`) of each event
emitted by the projection. This means that write amplification is a possibility, as each event that the
projection emits appends a separate event. As such, this option is not recommended for projections that emit a
lot of events, and you should enable only where necessary.
:::

::: tip 
Between EventStoreDB versions 3.8.0 and 4.0.2, this option was enabled by default when a projection
was created through the UI. If you have any projections created during this time frame, it's worth checking
whether this option is enabled.
:::

#### Max allowed writes in flight

<!-- TODO: Why is this not in the GUI for new projection? -->

The `MaxAllowedWritesInFlight` setting sets the maximum number of writes to allow for a projection. Because a
projection can write to multiple different streams, it's possible for the projection to send multiple writes
at the same time. This option sets the number of concurrent writes that a projection can perform.

By default, projections try to perform writes as quickly as they come. This can add a lot of pressure to a
node, especially for projections that emit to different streams. If you see your projections causing frequent
commit timeouts or slow reads, you can try lowering this value to see if there is any improvement.

::: tip 
Lower values may cause the projection to slow down as the number of writes are throttled, but the
trade-off for this is cleaner logs and fewer commit timeouts.
:::

By default, this is unbounded, allowing a projection to write as fast as it can.

#### Max write batch length

<!-- TODO: Why is this not in the GUI for new projection? -->

The `MaxWriteBatchLength` setting sets the maximum number of events the projection can write in a batch at a
time.

**Default:** `500` (events).

### Checkpoint options

Checkpoints store how far along a projection is in the streams it is processing from. There is a performance
overhead with writing a checkpoint, as it does more than append an event, and writing them too often can slow
projections down.

We recommend you try other methods of improving projections before changing these values, as checkpoints are
an important part of running projections.

#### Checkpoint after Ms

The `CheckpointAfterMs` setting prevents a new checkpoint from being written within a certain time frame from
the previous one. The setting is to keep a projection from writing too many checkpoints too quickly, something
that can happen in a busy system.

The default setting is 0 seconds, which means there is no limit to how quickly checkpoints can be written.

#### Checkpoint handled threshold

The `CheckpointHandledThreshold` setting controls the number of events that a projection can handle before
attempting to write a checkpoint. An event is considered handled if it actually passed through the
projection's filter. If the projection is set to checkpoint every 4,000 events, but it only reads from
the `foo` stream, the projection only checkpoints every 4,000 `foo` events.

**Default:** `4000` (events).

#### Checkpoint unhandled bytes threshold

The `CheckpointUnhandledBytesThreshold` setting specifies the number of bytes a projection can process before
attempting to write a checkpoint. Unhandled bytes are the events that are not processed by the projection
itself.

For example, if the projection reads from the `foo` stream, but writes from the `bar` stream comes through, a
checkpoint is written after this number of bytes have been processed. This prevents the projection from having
to read through a potentially large number of unrelated events again because none of them passed its filter.

**Default:** `10` (MiB).

### Processing options

#### Pending events threshold

The `PendingEventsThreshold` setting determines the number of events that can be pending before the projection
is paused. Pausing the projection stops the projection from reading, allowing it to finish with the current
events that are waiting to be processed. Once the pending queue has drained to half the threshold, the
projection starts reading again.

**Default:** `5000` (events).

## Debugging

[User projections you create in JavaScript](#user-defined-projections) have a bonus that debugging is easier
via any browser that ships with debugging capabilities. The screenshots in this document show the use of
Chrome, but we have tested debugging with all major browsers including Firefox, Edge and Safari.

### Logging from within a projection

For debugging purposes, projections includes a log method which, when called, sends messages to the configured
EventStoreDB logger (the default is `NLog`, to a file, and `stdout`).

You might find printing out the structure of the event body for inspection useful.

For example:

```javascript
fromStream('$stats-127.0.0.1:2113')
    .when({
        $any: function (s, e) {
            log(JSON.stringify(e));
        }
    })
```

#### Creating a sample projection for debugging purposes

Filename: _stats-counter.json_

Contents:

```javascript
fromStream('$stats-127.0.0.1:2113')
    .when({
        $init: function () {
            return {
                count: 0
            }
        },
        $any:  function (s, e) {
            s.count += 1;
        }
    })
```

You create the projection by making a call to the API and providing it with the definition of the projection.

```bash:no-line-numbers
curl -i -d@stats-counter.json \
  http://localhost:2113/projections/continuous?name=stats-counter%26type=js%26enabled=true%26emit=true%26trackemittedstreams=true \
  -u admin:changeit
```

### Debugging your first projection

Once the projection is running, open your browser and enable the developer tools. Once you have the developer
tools open, visit your projection URL and you should see a button labelled _Debug_.

::: card
![Projections Debugging Part 1](./images/projections_debugging_part_1.png)
:::

After clicking the projection "Debug" button, you see the debugging interface with the definition of the
projection and information about the events the projection is processing on the right-hand side.

At the top there are a couple of buttons to take note of, specifically the _Run Step_ and _Update_ buttons.
You use _Run Step_ to step through the event waiting in the queue, placing you in projection debugging mode.
The _Update_ button provides you with a way to update the projection definition without having to go back to
the projection itself and leave the context of the debugger.

::: card
![Projections Debugging Part 2](./images/projections_debugging_part_2.png)
:::

If the _Run Step_ button is not greyed out and you click it, the browser has hit a breakpoint.

::: card
![Projections Debugging Part 3](./images/projections_debugging_part_3.png)
:::

You are now able to step through the projection, the important method to step into is
the `handler(state, eventEnvelope)` method.

## Projections settings

Settings in this section concern projections that are running on the server.

::: warning 
Server-side projections impact the performance of the EventStoreDB server. For example, some
standard [system projections](#system-projections) like Category or Event Type projections produce new (link)
events that are stored in the database in addition to the original event. This effectively doubles or triples
the number of events appended and therefore creates pressure on the IO of the server node. We often call this
effect "write amplification".
:::

### Run projections

The `RunProjections` option tells the server if you want to run all projections, only system projections or no
projections at all. Keep in mind that the `StartSystemProjections` setting has no effect on custom
projections.

The option accepts three values: `None`, `System` and `All`.

When the option value is set to `None`, the projections subsystem of EventStoreDB will be completely disabled
and the Projections menu in the Admin UI will be disabled.

By using the `System` value for this option, you can instruct the server to enable system projections when the
server starts. However, system projections will only start if the `StartStandardProjections` option is set
to `true`. When the `RunProjections` option value is `System` (or `All`) but the `StartSystemProjections`
option value is `false`, system projections will be enabled but not start. You can start them later manually
via the Admin UI or via an API call.

Finally, you can set `RunProjections` to `All` and it will enable both system and custom projections.

| Format               | Syntax                       |
|:---------------------|:-----------------------------|
| Command line         | `--run-projections`          |
| YAML                 | `RunProjections`             |
| Environment variable | `EVENTSTORE_RUN_PROJECTIONS` |

**Default**: `None`, all projections are disabled by default.

Accepted values are `None`, `System` and `All`.

### Projection threads

Projection threads are used to make calls in to the V8 JavaScript engine, and coordinate dispatching
operations back into the main worker threads of the database. While they carry out none of the operations
listed directly, they are indirectly involved in all of them.

The primary reason for increasing the number of projection threads is projections which perform a large amount
of CPU-bound processing. Projections are always eventually consistent - if there is a mismatch between egress
from the database log and processing speed of projections, the window across which the latest events have not
been processed promptly may increase. Too many projection threads can end up with increased context switching
and memory use, since a V8 engine is created per thread.

There are three primary influences over projections lagging:

- Large number of writes, outpacing the ability of the engine to process them in a timely fashion.
- Projections which perform a lot of CPU-bound work (heavy calculations).
- Projections which result in a high system write amplification factor, especially with latent disks.

Use the `ProjectionThreads` option to adjust the number of threads dedicated to projections.

| Format               | Syntax                          |
|:---------------------|:--------------------------------|
| Command line         | `--projection-threads`          |
| YAML                 | `ProjectionThreads`             |
| Environment variable | `EVENTSTORE_PROJECTION_THREADS` |

**Default**: `3`

### Fault out of order projections

It is possible that in some cases a projection would get an unexpected event version. It won't get an event
that precedes the last processed event, such a situation is very unlikely. But, it might get the next event
that doesn't satisfy the `N+1` condition for the event number. The projection expects to get an event
number `5` after processing the event number `4`, but eventually it might get an event number `7` because
events `5` and `6` got deleted and scavenged.

The projections engine can keep track of the latest processed event for each projection. It allows projections
to guarantee ordered handling of events. By default, the projections engine ignore ordering failures like
described above. You can force out of order projections to fail by setting the `FailOutOfOrderProjections`
to `true`.

| Format               | Syntax                                      |
|:---------------------|:--------------------------------------------|
| Command line         | `--fault-out-of-order-projections`          |
| YAML                 | `FaultOutOfOrderProjections`                |
| Environment variable | `EVENTSTORE_FAULT_OUT_OF_ORDER_PROJECTIONS` |

**Default**: `false`
