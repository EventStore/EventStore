# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]


### Changed
- Update UI and submodule [EventStore#2493](https://github.com/EventStore/EventStore/pull/2493)
- gRPC Leader Not Found Exception will now return host/port in the trailers [EventStore#2491](https://github.com/EventStore/EventStore/pull/2491)
- More changes to support DNS endpoints in the Client API. [EventStore#2487](https://github.com/EventStore/EventStore/pull/2487)
- Removed UseSslConnection from the Tcp Client API Connection Settings Builder and replaced it with DisableTls and DisableServerCertificateValidation to resemble the options on the server more closely. [EventStore#2503](https://github.com/EventStore/EventStore/pull/2503)
- Set UseSslConnection=false and ValidateServer=false in connection string tests where required [EventStore#2505](https://github.com/EventStore/EventStore/pull/2505)
- Patch the version files when building the docker container so that the logs reflect that information. [EventStore#2512](https://github.com/EventStore/EventStore/pull/2512)
- Write leader's instance ID in epoch record. Pass on the epoch record's leader's instance id and each node's gossip information during elections to the leader of elections to determine more accurately if the previous leader is still alive when choosing the best leader candidate. [EventStore#2454](https://github.com/EventStore/EventStore/pull/2454)

### Removed
- Internal http endpoint [EventStore#2479](https://github.com/EventStore/EventStore/pull/2479)

### Fixed
- When starting Event Store without an Index Path specified (as is the case when running in memory), the server would crash with a `NullReferenceException`. [EventStore#2502](https://github.com/EventStore/EventStore/pull/2502)
- Compiling EventStore in Debug Mode [EventStore#2509](https://github.com/EventStore/EventStore/pull/2509)
- Test client not respecting --tls-validate-server=False [EventStore#2506](https://github.com/EventStore/EventStore/pull/2506)
- Logging `Object synchronization method was called from an unsynchronized method` as a warning instead of fatal when shutting down EventStore [EventStore#2516](https://github.com/EventStore/EventStore/pull/2516)
- VNodeState in cluster.proto not matching EventStore.Core.Data.VNodeState [EventStore#2518](https://github.com/EventStore/EventStore/pull/2518)

### Added
- jwt token support [EventStore#2510](https://github.com/EventStore/EventStore/pull/2510)

## [20.6.0 - Release Candidate] - 2020-05-15
### Changed
- HTTP read requests to `/streams/$scavenges/` are done via AdminController. [#2310](https://github.com/EventStore/EventStore/pull/2310)
- `/streams/$scavenges/{scavengeId}/` now maps to `/streams/$scavenges-{scavengeId}/`. [#2310](https://github.com/EventStore/EventStore/pull/2310)
- Start View Change Proof Timer on System Initialized only. [#2366](https://github.com/EventStore/EventStore/pull/2366)
- Replace byte[] with ReadOnlyMemory<byte> to reduce allocations. [#2308](https://github.com/EventStore/EventStore/pull/2308)
- Don't write $ProjectionDeleted events for queries when they're deleted [EventStore#2377](https://github.com/EventStore/EventStore/pull/2377)
- Prepared the authorization interfaces for plugin extraction [EventStore#2385](https://github.com/EventStore/EventStore/pull/2385)
- Remove unused code from MiniNode used in tests [EventStore#2401](https://github.com/EventStore/EventStore/pull/2401)
- Correct the xml documentation for the TCP Client settings builders. [EventStore#2393](https://github.com/EventStore/EventStore/pull/2393)
- Removed the constraint in AlreadyCommitted for Log Position to be positive. [EventStore#2404](https://github.com/EventStore/EventStore/pull/2404)
- Set the default for the write timeout [EventStore#2410](https://github.com/EventStore/EventStore/pull/2410)
- Replaced `UseCustomHttpClient` in the ConnectionSettingsBuilder for the TCP client with `UseCustomHttpMessageHandler` [EventStore#2419](https://github.com/EventStore/EventStore/pull/2419)
- Ensure that the leader is still in a leader state when gossip has changed. If not, start elections. [EventStore#2418](https://github.com/EventStore/EventStore/pull/2418)
- Allow a projection to be able to checkpoint regardless of whether the event filter passes. [EventStore#2428](https://github.com/EventStore/EventStore/pull/2428)
- Authorization and Authentication plugin interfaces have been removed and re-included as a nuget package from EventStore.Plugins [EventStore#2409](https://github.com/EventStore/EventStore/pull/2409)
- Authentication Provider Factory now takes a Serilog ILogger [EventStore#2409](https://github.com/EventStore/EventStore/pull/2409)
- Make read-only replicas independent of elections service and use gossip updates to determine leader changes. [EventStore#2427](https://github.com/EventStore/EventStore/pull/2427)
- Send the last replication checkpoint when a node subscribes to the leader [EventStore#2445](https://github.com/EventStore/EventStore/pull/2445)
- License information [EventStore#2439](https://github.com/EventStore/EventStore/pull/2439)
- Authorization and Authentication Plugins no longer require MEF [EventStore#2457](https://github.com/EventStore/EventStore/pull/2457)
- Appends will now return a oneof response types which currently is either a `Success` or a `WrongExpectedVersion`. [EventStore#2463](https://github.com/EventStore/EventStore/pull/2463)
- Enable the Test Client to connect to a dns or ip endpoint [EventStore#2474](https://github.com/EventStore/EventStore/pull/2474)
- `AuthToken` field and constructor added to `UserCredentials` [EventStore#2471](https://github.com/EventStore/EventStore/pull/2471)
- The connection settings for the Tcp Client now accepts Dns EndPoint as a means to connect to the nodes in the cluster. [EventStore#2480](https://github.com/EventStore/EventStore/pull/2480)
- Authenticate requests as the system user if they provide a valid client certificate with the provided trusted root certificate [EventStore#2475](https://github.com/EventStore/EventStore/pull/2475)
- Require a system user for gossip update, and for all election operations [EventStore#2475](https://github.com/EventStore/EventStore/pull/2475)
- Update UI submodule as well as the pre-built UI. [EventStore#2490](https://github.com/EventStore/EventStore/pull/2490)

### Removed
- Unused HTTP messages. [#2362](https://github.com/EventStore/EventStore/pull/2363)
- Removed better ordering option. [#2368](https://github.com/EventStore/EventStore/pull/2368)
- Application Defines [EventStore#2441](https://github.com/EventStore/EventStore/pull/2441)
- Force option [EventStore#2442](https://github.com/EventStore/EventStore/pull/2442)
- Get Gossip from the Gossip Controller as this is now done over gRPC [EventStore#2458](https://github.com/EventStore/EventStore/pull/2458)

### Added
- Supports for pull request linting and automatic changelog update [EventStore#2391](https://github.com/EventStore/EventStore/pull/2391)
- The following options have been added `AuthorizationType` and `AuthorizationConfig` to mirror that of the existing `AuthenticationType` and `AuthenticationConfig`. [EventStore#2385](https://github.com/EventStore/EventStore/pull/2385)
- A mandatory configuration parameter named: `TrustedRootCertificatesPath`. The certificate store will be expanded with the root certificates in this path before certificate validation. For server certificate validation, trust is restricted to system certificates + the specified root certificates but for client certificate validation, trust is restricted only to the specified root certificates. Before this change, all root certificates installed on the system were trusted. [EventStore#2335](https://github.com/EventStore/EventStore/pull/2335)
- Client certificate validation to the internal HTTP interface which is used for gossip/elections [EventStore#2335](https://github.com/EventStore/EventStore/pull/2335)
- Discover an existing leader through gossip updates when a read-only replica starts up instead of triggering elections. [EventStore#2417](https://github.com/EventStore/EventStore/pull/2417)
- Login endpoint (admin/login) [EventStore#2409](https://github.com/EventStore/EventStore/pull/2409)
- Features collection on the info endpoint [EventStore#2409](https://github.com/EventStore/EventStore/pull/2409)
- Operations proto contract and implementation [EventStore#2446](https://github.com/EventStore/EventStore/pull/2446)
- Max Truncation Safety Feature to avoid large unexpected truncations due to misconfiguration [EventStore#2436](https://github.com/EventStore/EventStore/pull/2436)
- New route in PersistentSubscriptionController to view parked messages /subscriptions/viewparkedmessages/{stream}/{group} [EventStore#2392](https://github.com/EventStore/EventStore/pull/2392)
- A new project `EventStore.NETCore.Compatibility` which takes the code for `System.UriTemplate` from .NET Framework 4.8 reference source (MIT-licensed) instead of depending on `SimpleSyndicate.UriTemplate` (no license) [EventStore#2439](https://github.com/EventStore/EventStore/pull/2439)
- TCP jwt authorization on the wire [EventStore#2449](https://github.com/EventStore/EventStore/pull/2449)
- Push `EventStore.ClientAPI` and `EventStore.ClientAPI.Embedded` to GitHub Package Registry [EventStore#2462](https://github.com/EventStore/EventStore/pull/2462)
- Provide the ability to specify `DnsEndPoint`s as part of gossip seeds. [EventStore#2455](https://github.com/EventStore/EventStore/pull/2455)
- Provide the ability to specify `DnsEndPoint`s as advertise information [EventStore#2455](https://github.com/EventStore/EventStore/pull/2455)
- Extended the proto contract for read responses to include Stream Not Found. [EventStore#2473](https://github.com/EventStore/EventStore/pull/2473)
- With the terminology changes we made, the class names have been changed but we just missed renaming the files. [EventStore#2383](https://github.com/EventStore/EventStore/pull/2383)
- Fix bug introduced by f87b317b78248638aba18a6173e63b809ece5d66 [EventStore#2406](https://github.com/EventStore/EventStore/pull/2406)
- When restarting each node one at a time, should keep db as the test fixture will remove the root directory. This test fails the CI quite often. [EventStore#2482](https://github.com/EventStore/EventStore/pull/2482)
- Use TrySetResult instead of SetResult so that exceptions are not thrown if setting result twice [EventStore#2406](https://github.com/EventStore/EventStore/pull/2406)

### Fixed
- Connect to existing master when a node starts up instead of triggering unnecessary elections if a quorum of nodes is already present. [EventStore#2386](https://github.com/EventStore/EventStore/pull/2386)
- Add server certificate validation when follower forwards requests over leader's external HTTP. [EventStore#2408](https://github.com/EventStore/EventStore/pull/2408)
- Fix ArgumentNullException in ByCorrelationId standard projection when the event's metadata is null. [EventStore#2430](https://github.com/EventStore/EventStore/pull/2430)
- WhatIf option will now terminate the application if set. [EventStore#2432](https://github.com/EventStore/EventStore/pull/2432)
- No longer raise an exception when reading a linked event with a bad payload. [EventStore#2424](https://github.com/EventStore/EventStore/pull/2424)
- In the TCP client, prevent the first operation from taking a huge amount of time in some situation. [EventStore#2440](https://github.com/EventStore/EventStore/pull/2440)
- Do not wait for acks/nacks after a potential subscription failure. The task might never complete. [EventStore#2437](https://github.com/EventStore/EventStore/pull/2437)
- Read-only replicas can be stuck in Subscribing to Leader mode [EventStore#2427](https://github.com/EventStore/EventStore/pull/2427)
- NodePreference.Leader is not always honored in ClusterDnsEndPointDiscoverer [EventStore#2422](https://github.com/EventStore/EventStore/pull/2422)
- Initialize a Console Logger when the application initializes [EventStore#2444](https://github.com/EventStore/EventStore/pull/2444)
- Skip emitted events during recovery if their linked event no longer exists. [EventStore#2447](https://github.com/EventStore/EventStore/pull/2447)
- Overflow bug when setting file size based on number of midpoints [EventStore#2450](https://github.com/EventStore/EventStore/pull/2450)
- Off by one in GetDepth() which can cause less midpoints to be computed when increasing IndexCacheDepth in some cases [EventStore#2450](https://github.com/EventStore/EventStore/pull/2450)
- Broken CI Build [EventStore#2466](https://github.com/EventStore/EventStore/pull/2466)
- the default ArraySegment's data is null, not an empty array. [EventStore#2486](https://github.com/EventStore/EventStore/pull/2486)

### Bug
- Event Counts in GRPC Transport no longer wrap to negative values if > int.MaxValue [EventStore#2452](https://github.com/EventStore/EventStore/pull/2452)

### Updated
- Changed the default schema on the projection and users manager to be https. [EventStore#2459](https://github.com/EventStore/EventStore/pull/2459)

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
