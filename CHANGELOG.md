# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]
### Changed
- HTTP read requests to `/streams/$scavenges/` are done via AdminController. [#2310](https://github.com/EventStore/EventStore/pull/2310)
- `/streams/$scavenges/{scavengeId}/` now maps to `/streams/$scavenges-{scavengeId}/`. [#2310](https://github.com/EventStore/EventStore/pull/2310)
- Start View Change Proof Timer on System Initialized only. [#2366](https://github.com/EventStore/EventStore/pull/2366)
- Replace byte[] with ReadOnlyMemory<byte> to reduce allocations. [#2308](https://github.com/EventStore/EventStore/pull/2308)
- Don't write $ProjectionDeleted events for queries when they're deleted [EventStore#2377](https://github.com/EventStore/EventStore/pull/2377)
- Prepared the authorization interfaces for plugin extraction [EventStore#2385](https://github.com/EventStore/EventStore/pull/2385)
- Remove unused code from MiniNode used in tests [EventStore#2401](https://github.com/EventStore/EventStore/pull/2401)
- Correct the xml documentation for the TCP Client settings builders. [EventStore#2393](https://github.com/EventStore/EventStore/pull/2393)

### Removed
- Unused HTTP messages. [#2362](https://github.com/EventStore/EventStore/pull/2363)
- Removed better ordering option. [#2368](https://github.com/EventStore/EventStore/pull/2368)

### Added
- Supports for pull request linting and automatic changelog update [EventStore#2391](https://github.com/EventStore/EventStore/pull/2391)
- The following options have been added `AuthorizationType` and `AuthorizationConfig` to mirror that of the existing `AuthenticationType` and `AuthenticationConfig`. [EventStore#2385](https://github.com/EventStore/EventStore/pull/2385)

### With the terminology changes we made, the class names have been changed but we just missed renaming the files. [EventStore#2383](https
- //github.com/EventStore/EventStore/pull/2383)

## [6.0.0 - Preview 3] - 2020-03-11
The changelog below is a summary of the all of the preview releases.

### Added
- New gRPC .NET client added. 
- The ability to filter reads by regex or prefix for both stream name and event type.
- New options for certicates. Can be provided as PKCS, public/private key pair and windows store.
- Introduction of a liveness health check at `{server_address}/health/live`.
- Improvements to projection writes.
- Read-only replica. A node that will not partake in elections and is non promotable. Started with the `--read-only-replica` argument
- The ability to resign master using `{server_address}/admin/node/resign`.
- Added in the ability to set node priority with `{server_address}/admin/node/priority/{priority}`.

### Changed
- Change of cluster role names from master/slave to leader/follower.
- Both client and server have been moved over to .NET Core.

### Deprecated
- ATOM and TCP are being deprecated. These can be re-enabled with `--enable-external-tcp` and `--enable-atom-pub-over-http`.
- Clone nodes have been deprecated and replaced with read only replicas. Can be re-enabled using `--unsafe-allow-surplus-nodes`

### Removed
- Support for Event Store server on macOS.
- Undocumented projection selectors.
- The requirement for mono.

### Security
- TLS is enabled by default for internal node communication. Can be disabled by `--disable-internal-tls`.
- All external HTTP is HTTPS by default. 
