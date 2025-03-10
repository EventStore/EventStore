---
order: 5
---

# Release Notes

## [24.10.4](https://github.com/EventStore/EventStore/releases/tag/v24.10.4) - 6 Mar 2025

### Reduce allocations when projections validate json (PR [#4881](https://github.com/EventStore/EventStore/pull/4881))

This reduces memory pressure when running projections, especially on databases with larger events.

There is a change in behaviour between 24.10.4 and previous versions.

Previously unquoted keys e.g. `{ foo : "bar" }` were erroneously considered valid json, and would be processed by the projections. This is not the case starting from 24.10.4.

Projections won’t fault after an upgrade if they previously emitted an event based off of a now-invalid event.

### Add an option to limit the size of events appended over gRPC and HTTP (PR [#4882](https://github.com/EventStore/EventStore/pull/4882))

The `MaxAppendEventSize` option has been added to limit the size of individual events in an append request over gRPC and HTTP. The default for this new option is `int.MaxValue` (no limit). Events appended using the AtomPub HTTP API still may not exceed 4mb. Events written by internal systems or services such as projections are unaffected.

This option is different from `MaxAppendSize`, which limits the size of entire append requests over gRPC.

A new RPC exception `maximum-append-event-size-exceeded` was added as part of this, and is returned when any event in an append batch exceeds the configured `MaxAppendEventSize`.

**Note:** The calculation of event size for events appended over gRPC was fixed as part of this, which could result in a change in behaviour for appending batches of events over gRPC. Previously, the event size was calculated off of the event's data, excluding the size of the event's metadata or event type. This means that append requests which previously did not trip the `MaxAppendSize` check could fail on 24.10.4.

### Add an option to limit the size of the projection state (PR [#4884](https://github.com/EventStore/EventStore/pull/4884))

The `MaxProjectionStateSize` option has been added to configure the maximum size of a projection's state before the projection should fault. This defaults to `int.MaxValue` (no limit).

A projection will now fault if the total size of the projection’s state and result exceeds the `MaxProjectionStateSize`. This replaces the warning log for state size in `CoreProjectionCheckpointManager`.

**Note:** In certain cases, a projection’s state and result will be set to the same value and be written to the projection state event twice. This can result in the `MaxProjectionStateSize` being exceeded even when the projection state is half the size of the configured maximum.

## [24.10.3](https://github.com/EventStore/EventStore/releases/tag/v24.10.3) - 27 Feb 2025

### Fix getting projection statistics over gRPC for faulted projections (PR #4863)

Prior to this fix, projection statistics could not be retrieved via gRPC if one of the projections immediately transitions into a faulted state.

### Fix possibility of a gRPC write hanging clientside if a leader election occurs (PR #4858)

Prior to this fix, if a gRPC write was pending at the time of a leader election, the gRPC call could be terminated successfully instead of in a failed state. On the dotnet client this results in the Append Task call hanging indefinitely, other clients may be affected as well.

## [24.10.2](https://github.com/EventStore/EventStore/releases/tag/v24.10.2) - 13 Feb 2025

### Handle replayed messages when retrying events in a persistent subscription (PR #4777)

This fixes an issue with persistent subscriptions where retried messages may be missed if they are retried after a parked message is already in the buffer. This can happen if a user triggers a replay of parked messages while there are non-parked messages timing out and being retried.

When this occurs, an error is logged for each message that is missed:

```
Error while processing message EventStore.Core.Messages.SubscriptionMessage+PersistentSubscriptionTimerTick in queued handler 'PersistentSubscriptions'.
System.InvalidOperationException: Operation is not valid due to the current state of the object.
```

If a retried message is missed in this way, the consumer will never receive the message. In order to recover and receive these messages again, the persistent subscription will need to be reset.

### Validate against attempts to set metadata for the "" stream (PR #4799)

Empty string (“”) has never been a valid stream name. Attempting to set the metadata for it results in an attempt to write to the stream “$$” which, until now, has been a valid stream name.

However, writing to “$$” involves checking on the “” stream, to see if it is soft deleted. This results in the storage writer exiting which shuts down the server to avoid a 'sick but not dead' scenario

“$$” is now an invalid stream name, and so any attempt to write to it is rejected at an early stage.

### Fix EventStoreDB being unable to start on Windows 2016 (PR #4765)

EventStoreDB would crash when running on Windows 2016 with the following error:

```
[ 1488, 1,16:45:11.368,FTL] EventStore Host terminated unexpectedly.
System.TypeInitializationException: The type initializer for 'EventStore.Core.Time.Instant' threw an exception.
---> System.Exception: Expected TicksPerSecond (1853322) to be an exact multiple of TimeSpan.TicksPerSecond (10000000)
at EventStore.Core.Time.Instant..cctor() in D:\a\TrainStation\TrainStation\EventStore\src\EventStore.Core\Time\Instant.cs:line 21
--- End of inner exception stack trace ---
```

This was caused by a bug when converting ticks between Stopwatch.Frequency and TimeSpan.TicksPerSecond.

### Fixes to stats and metrics

Fixed the following issues:

- On Linux the disk usage/capacity were showing the values for the disk mounted at `/` even if the database was on a different disk (PR #4759)
- Align histogram buckets with the dashboard (PR #4811)
- Fix some disk IO stats being 0 on linux due to the assumption that the `/proc/<pid>/io` file was in a particular order and returned early without reading all the values (PR #4818)

### Support paging in persistent subscriptions (PR #4785)

The persistent subscription UI now pages when listing all persistent subscriptions rather than loading all of them at once. By default, the UI shows the persistent subscription groups for the first 100 streams, and refreshes every second. These options can be changed in the UI.

A count and offset can now be specified when getting all persistent subscription stats through the HTTP API: `/subscriptions?count={count}&offset={offset}`.

The response of `/subscriptions` (without the query string) is unchanged.

## [24.10.1](https://github.com/EventStore/EventStore/releases/tag/v24.10.1) - 18 Dec 2024

### Up to 100 license entitlements are fetched (PR #4670)

Previously only 10 license entitlements were returned, which meant that entitlements were missing on the commercial license policies that have more than 10 entitlements.

If you are using a commercial trial of 24.10 you will need to upgrade to 24.10.1 to transition on to the full license.

### SLOW QUEUE messages are logged by default again (PR #4660)

Slow bus messages were still logged, but the logging of slow queue messages had been accidentally disabled by default.

### Update dependency patch version due to CVEs (PR #4674)

`System.Private.Uri` has been upgraded from 4.3.0 to 4.3.2 (patch upgrade) due to `GHSA-5f2m-466j-3848` and `GHSA-xhfc-gr8f-ffwc`

## 24.10.0

Link to the [What's new](./whatsnew.md)