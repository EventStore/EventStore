---
order: 5
---

# Release Notes

*These should be scraped from https://github.com/EventStore/EventStore/releases*

## 24.10.4 - 

- [Packages](https://cloudsmith.io/~eventstore/repos/eventstore/packages/?q=24.10.4)

## Notable changes

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

## Other changes

### Changed
* [KDB-697] Removed buffer copy by @sakno in https://github.com/EventStore/EventStore/pull/4883

**Full Changelog**: https://github.com/EventStore/EventStore/compare/v24.10.3...v24.10.4

## 24.10.3

- [Packages](https://cloudsmith.io/~eventstore/repos/eventstore/packages/?q=24.10.3)

## Notable changes

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

## Other changes

### Changed
* [KDB-697] Removed buffer copy by @sakno in https://github.com/EventStore/EventStore/pull/4883

**Full Changelog**: https://github.com/EventStore/EventStore/compare/v24.10.2...v24.10.3

## 24.10.2

- [Packages](https://cloudsmith.io/~eventstore/repos/eventstore/packages/?q=24.10.2)

## Notable changes

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

## Other changes

### Changed
* [KDB-697] Removed buffer copy by @sakno in https://github.com/EventStore/EventStore/pull/4883

**Full Changelog**: https://github.com/EventStore/EventStore/compare/v24.10.1...v24.10.2

## 24.10.1

- [Packages](https://cloudsmith.io/~eventstore/repos/eventstore/packages/?q=24.10.1)

## Notable changes

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

## Other changes

### Changed
* [KDB-697] Removed buffer copy by @sakno in https://github.com/EventStore/EventStore/pull/4883

**Full Changelog**: https://github.com/EventStore/EventStore/compare/v24.10.0...v24.10.1


## 24.10.0

Link to the [What's new](./whatsnew.md)