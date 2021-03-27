# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Introduce compatibility mode. [EventStore#2796](https://github.com/EventStore/EventStore/pull/2796)
- Add LegacyGossipDiscovery. [EventStore#2744](https://github.com/EventStore/EventStore/pull/2744)

### Fixed
- Time out gossip discovery on the TCP client if the task does not complete [EventStore#2821](https://github.com/EventStore/EventStore/pull/2821)
- Regression in TCP connection introduced by commit: cd2aa67926dd06d48c894888d547d92e0d2b9cc1 from PR: https://github.com/EventStore/EventStore/pull/2772 [EventStore#2834](https://github.com/EventStore/EventStore/pull/2834)
- Mutex being released on wrong thread resulting in an annoying log message on shutdown [EventStore#2838](https://github.com/EventStore/EventStore/pull/2838)
- Keep alive timeout check [EventStore#2861](https://github.com/EventStore/EventStore/pull/2861)
- TestClient not exiting after executing `--command`, which prevents it from being automated in an easy way. [EventStore#2871](https://github.com/EventStore/EventStore/pull/2871)

### Based on the agreement made with @jageall (see notes here https
- //github.com/EventStore/advocacy/issues/89). I'm sending the first PR moving PR docs for the database. [EventStore#2831](https://github.com/EventStore/EventStore/pull/2831)

### Changed
- ValidateServer also sets whether HTTP certificate validation is enabled. [EventStore#2832](https://github.com/EventStore/EventStore/pull/2832)
- Make Microsoft.NETFramework.ReferenceAssemblies reference private [EventStore#2859](https://github.com/EventStore/EventStore/pull/2859)

### changed
- minver prefix for tcp clients [EventStore#2846](https://github.com/EventStore/EventStore/pull/2846)

## [21.2.0] Server - 2021-02-26

### Added
- --stream-info-cache-capacity option to allow setting the cache capacity of the ReadIndex. [EventStore#2762](https://github.com/EventStore/EventStore/pull/2762)
- Parked message count is now available on persistent subscription stats [EventStore#2792](https://github.com/EventStore/EventStore/pull/2792)
- Content Type Validation to projections which will allow projections to only handle valid json events if isJson is set to true [EventStore#2812](https://github.com/EventStore/EventStore/pull/2812)
- script to check for proto changes [EventStore#2817](https://github.com/EventStore/EventStore/pull/2817)
- Server Support for gRPC Keep Alive [EventStore#2819](https://github.com/EventStore/EventStore/pull/2819)

### Changed
- Updated internal dependencies and added client builds for .NET 5.0 [EventStore#2764](https://github.com/EventStore/EventStore/pull/2764)
- GossipOnSingleNode is now on by default and the setting has been deprecated in config [EventStore#2818](https://github.com/EventStore/EventStore/pull/2818)
- Increased the maximum chunk count to patch issue with 25 logical TB.  [EventStore#2822](https://github.com/EventStore/EventStore/pull/2822)

### Fixed
- Proactively send heartbeat requests to the remote party if no data was sent within the last heartbeat interval [EventStore#2772](https://github.com/EventStore/EventStore/pull/2772)
- Linux/macOS build.sh script for .NET 5.0 [EventStore#2774](https://github.com/EventStore/EventStore/pull/2774)
- Windows build.ps1 script for .NET 5.0 [EventStore#2776](https://github.com/EventStore/EventStore/pull/2776)
- Performance counter error message on linux / macOS [EventStore#2775](https://github.com/EventStore/EventStore/pull/2775)
- --insecure has stopped working after targeting .NET 5.0 [EventStore#2779](https://github.com/EventStore/EventStore/pull/2779)
- failing test [EventStore#2788](https://github.com/EventStore/EventStore/pull/2788)
- Track retry count for persistent subscription messages after a client has lost connection. [EventStore#2797](https://github.com/EventStore/EventStore/pull/2797)
- failing test [EventStore#2800](https://github.com/EventStore/EventStore/pull/2800)
- Permission Denied when performing privileged commands on a follower [EventStore#2803](https://github.com/EventStore/EventStore/pull/2803)
- Check for old/replayed events only if the event passes the event filter [Projections] [EventStore#2809](https://github.com/EventStore/EventStore/pull/2809)
- Prevent a projection checkpoint from being emitted at same position twice [EventStore#2824](https://github.com/EventStore/EventStore/pull/2824)

## [20.10.0] - 2020-12-16

### Fixed
- Handle CORS requests first, followed by authentication provider endpoints, then legacy endpoints [EventStore#2693](https://github.com/EventStore/EventStore/pull/2693)
- Memory/disk space issues during large cascading index merges (especially when index cache depth is high) [EventStore#2700](https://github.com/EventStore/EventStore/pull/2700)
- Casting of TcpConnection when getting replication stats [EventStore#2729](https://github.com/EventStore/EventStore/pull/2729)
- Stackoverflow when sending large amounts of data over secure TCP connections [EventStore#2730](https://github.com/EventStore/EventStore/pull/2730)
- #2734 incorrectly shared operations between threads [EventStore#2747](https://github.com/EventStore/EventStore/pull/2747)
- ci.yml following set-env deprecation [EventStore#2749](https://github.com/EventStore/EventStore/pull/2749)
- EventStore/home#263 [EventStore#2745](https://github.com/EventStore/EventStore/pull/2745)
- incorrect error message in catchup subscription [EventStore#2751](https://github.com/EventStore/EventStore/pull/2751)
- Corrected `AssemblyVersion` [EventStore#2756](https://github.com/EventStore/EventStore/pull/2756)
- Incorrect error message in catchup subscription [EventStore#2751](https://github.com/EventStore/EventStore/pull/2751)
- v20 clients can discover v20.x and v5.x servers [EventStore#2719](https://github.com/EventStore/EventStore/pull/2719)

### Changed
- Read operations are now backed by System.Threading.Channels [EventStore#2712](https://github.com/EventStore/EventStore/pull/2712)
- Add the certificate subject to the log message printed when there is a certificate validation error [EventStore#2746](https://github.com/EventStore/EventStore/pull/2746)
- DNS Seeds Are No Longer Resolved to IP Addresses [EventStore#2753](https://github.com/EventStore/EventStore/pull/2753)
- Update the UI with replication stats fix [EventStore#2726](https://github.com/EventStore/EventStore/pull/2726)
- Add checkpoint based tracking of proposed epoch numbers [EventStore#2745](https://github.com/EventStore/EventStore/pull/2745)

## [20.6.1] - 2020-09-28

### Changed
- Log level from Verbose to Debug/Information for important messages [EventStore#2538](https://github.com/EventStore/EventStore/pull/2538)
- Change options that refers to disabling tls to explicitly refer to disabling tcp tls. [EventStore#2537](https://github.com/EventStore/EventStore/pull/2537)
- Adjust deprecation warning from referring to 20.02 to 20.6.0 [EventStore#2567](https://github.com/EventStore/EventStore/pull/2567)
- Do not print stack traces when an invalid configuration is encountered. [EventStore#2578](https://github.com/EventStore/EventStore/pull/2578)
- Instead of always giving system access over HTTP when running with --insecure (since no client certificate is provided), only pre-authorize the gossip and election routes with system access [EventStore#2587](https://github.com/EventStore/EventStore/pull/2587)
- Don't treat unresolved links as deleted linkTo events when checking for deleted partitions in projections. [EventStore#2586](https://github.com/EventStore/EventStore/pull/2586)
- When --dev is set, disable TLS on all interfaces instead of setting development certificates [EventStore#2581](https://github.com/EventStore/EventStore/pull/2581)
- Do not set --mem-db when --dev is set [EventStore#2581](https://github.com/EventStore/EventStore/pull/2581)
- Upgraded dotnet sdk to 3.1.301 [EventStore#2582](https://github.com/EventStore/EventStore/pull/2582)
- Enable v5 client cluster connectivity acceptance tests [EventStore#2554](https://github.com/EventStore/EventStore/pull/2554)
- Changed the default cluster gossip port from 30777 to 2113 [EventStore#2618](https://github.com/EventStore/EventStore/pull/2618)
- DisableInternalTcpTls has no effect, Insecure mode should be used to disable it [EventStore#2628](https://github.com/EventStore/EventStore/pull/2628)
- Disable authentication & authorization when --insecure is specified [EventStore#2614](https://github.com/EventStore/EventStore/pull/2614)
- gRPC reads will always try and read maxCount of events if it's not reached the end of the stream. [EventStore#2631](https://github.com/EventStore/EventStore/pull/2631)
- MessageTimeout and CheckpointAfter in persistent subscription settings are now expressed in milliseconds. [EventStore#2642](https://github.com/EventStore/EventStore/pull/2642)
- Updated startup logs to be more clear about security and interfaces. [EventStore#2656](https://github.com/EventStore/EventStore/pull/2656)
- Use AdvertiseHostToClientAs, AdvertiseHttpPortToClientAs and AdvertiseTcpPortToClientAs in the NotHandled.NotLeader response from the node. [EventStore#2665](https://github.com/EventStore/EventStore/pull/2665)
- Updated Pre-built UI to latest version [EventStore#2686](https://github.com/EventStore/EventStore/pull/2686)
- Responses to append will include expected revision / state sent from client [EventStore#2679](https://github.com/EventStore/EventStore/pull/2679)
- Update plugin version [EventStore#2690](https://github.com/EventStore/EventStore/pull/2690)

### Fixed
- Do not start other services if run is being skipped when --help or --version are specified [EventStore#2558](https://github.com/EventStore/EventStore/pull/2558)
- Prevent Stackoverflow when accepting too much data over a TCP connection on dotnet core [EventStore#2560](https://github.com/EventStore/EventStore/pull/2560)
- Improved output of CLI help [EventStore#2577](https://github.com/EventStore/EventStore/pull/2577)
- Log to default directory; args from CLI [EventStore#2574](https://github.com/EventStore/EventStore/pull/2574)
- Slow gRPC subscriptions [EventStore#2566](https://github.com/EventStore/EventStore/pull/2566)
- Handle successful link event resolution when projections emit events. [EventStore#2465](https://github.com/EventStore/EventStore/pull/2465)
- gRPC unable to read events from a truncated stream [EventStore#2631](https://github.com/EventStore/EventStore/pull/2631)
- Error on TCP operations after default user fails authentication [EventStore#2638](https://github.com/EventStore/EventStore/pull/2638)
- Wrong calculation of checkpoint interval for filtered subscriptions [EventStore#2608](https://github.com/EventStore/EventStore/pull/2608)
- Prevent gRPC errors when subscriptions are disposed [EventStore#2647](https://github.com/EventStore/EventStore/pull/2647)
- Do not do an exact check on certificate subject to match the Common Name [EventStore#2681](https://github.com/EventStore/EventStore/pull/2681)
- Removed cancellation race condition [EventStore#2682](https://github.com/EventStore/EventStore/pull/2682)
- Properly handle `LiveUntil` in `GrpcMessage.SendOverGrpc` and add a `Deadline` parameter [EventStore#2685](https://github.com/EventStore/EventStore/pull/2685)
- Requests with more than one url segment are correctly routed [EventStore#2691](https://github.com/EventStore/EventStore/pull/2691)
- Handle authentication provider endpoints first followed by legacy endpoints [EventStore#2694](https://github.com/EventStore/EventStore/pull/2694)
- Prevent clients from connecting to read only replicas which have not yet caught up [EventStore#2674](https://github.com/EventStore/EventStore/pull/2674) Thanks to @01100010011001010110010101110000 

### Added
- Option to set client certificate common name [EventStore#2572](https://github.com/EventStore/EventStore/pull/2572)
- --insecure flag to disable TLS on all interfaces (TCP & HTTP) to eliminate requirement for certificates to make it easier to run EventStoreDB [EventStore#2556](https://github.com/EventStore/EventStore/pull/2556)
- Ability to reload certificates by triggering the /admin/reloadconfig endpoint or by sending a SIGHUP signal (linux only) [EventStore#2590](https://github.com/EventStore/EventStore/pull/2590)
- Ability to load (or reload) default log level from EventStore config file [EventStore#2602](https://github.com/EventStore/EventStore/pull/2602)
- Logging around cases where the latest stream's prepare could not be read [EventStore#2613](https://github.com/EventStore/EventStore/pull/2613)
- Introduced the ability to restart the persistent subscriptions service [EventStore#2605](https://github.com/EventStore/EventStore/pull/2605)
- AdvertiseHostToClientAs, AdvertiseHttpPortToClientAs and AdvertiseTcpPortToClientAs to allow setting the gossip and TCP endpoints advertised to clients. [EventStore#2641](https://github.com/EventStore/EventStore/pull/2641)
- Docker-compose file [EventStore#2657](https://github.com/EventStore/EventStore/pull/2657)
- Allow external clients to discover supported authentication methods [EventStore#2637](https://github.com/EventStore/EventStore/pull/2637)

### Removed
- Terraform templates for generating a certificate authority and node certificates as we have an Event Store Certificate generation tool available. [EventStore#2653](https://github.com/EventStore/EventStore/pull/2653)
- Development mode [EventStore#2648](https://github.com/EventStore/EventStore/pull/2648)

## [20.6.0] - 2020-06-09

### Changed
- Update UI and submodule [EventStore#2493](https://github.com/EventStore/EventStore/pull/2493)
- gRPC Leader Not Found Exception will now return host/port in the trailers [EventStore#2491](https://github.com/EventStore/EventStore/pull/2491)
- More changes to support DNS endpoints in the Client API. [EventStore#2487](https://github.com/EventStore/EventStore/pull/2487)
- Removed UseSslConnection from the Tcp Client API Connection Settings Builder and replaced it with DisableTls and DisableServerCertificateValidation to resemble the options on the server more closely. [EventStore#2503](https://github.com/EventStore/EventStore/pull/2503)
- Set UseSslConnection=false and ValidateServer=false in connection string tests where required [EventStore#2505](https://github.com/EventStore/EventStore/pull/2505)
- Patch the version files when building the docker container so that the logs reflect that information. [EventStore#2512](https://github.com/EventStore/EventStore/pull/2512)
- Write leader's instance ID in epoch record. Pass on the epoch record's leader's instance id and each node's gossip information during elections to the leader of elections to determine more accurately if the previous leader is still alive when choosing the best leader candidate. [EventStore#2454](https://github.com/EventStore/EventStore/pull/2454)
- Updated the EventStore.Plugins version [EventStore#2521](https://github.com/EventStore/EventStore/pull/2521)
- Update Embedded Client Plugins Package to 20.6 [EventStore#2527](https://github.com/EventStore/EventStore/pull/2527)
- For gRPC use the `commit` and `prepare` positions from the `ResolvedEvent` instead of the `Transaction Position` as the `Prepare Position` and the `Log Position` in the case where the commit position is `null`. [EventStore#2522](https://github.com/EventStore/EventStore/pull/2522)
- Require IP or DNS SAN and CN=eventstoredb-node in client certificate to be assigned system role [EventStore#2513](https://github.com/EventStore/EventStore/pull/2513)
- Use CommitIndexed instead of CommitAck for the completion of the Write Request [EventStore#2529](https://github.com/EventStore/EventStore/pull/2529)
- Add the users in the $ops group the ability to restart the projection's subsystem. [EventStore#2526](https://github.com/EventStore/EventStore/pull/2526)
- Added HostStat.NET dependency to embedded client [EventStore#2534](https://github.com/EventStore/EventStore/pull/2534)
- Disable atomPub by default except when in dev mode. [EventStore#2531](https://github.com/EventStore/EventStore/pull/2531)
- Restructured stream name for future planned changes [EventStore#2530](https://github.com/EventStore/EventStore/pull/2530)

### Removed
- Internal http endpoint [EventStore#2479](https://github.com/EventStore/EventStore/pull/2479)

### Fixed
- When starting Event Store without an Index Path specified (as is the case when running in memory), the server would crash with a `NullReferenceException`. [EventStore#2502](https://github.com/EventStore/EventStore/pull/2502)
- Compiling EventStore in Debug Mode [EventStore#2509](https://github.com/EventStore/EventStore/pull/2509)
- Test client not respecting --tls-validate-server=False [EventStore#2506](https://github.com/EventStore/EventStore/pull/2506)
- Logging `Object synchronization method was called from an unsynchronized method` as a warning instead of fatal when shutting down EventStore [EventStore#2516](https://github.com/EventStore/EventStore/pull/2516)
- VNodeState in cluster.proto not matching EventStore.Core.Data.VNodeState [EventStore#2518](https://github.com/EventStore/EventStore/pull/2518)
- EventStore.Client.Embedded missing package dependencies [EventStore#2496](https://github.com/EventStore/EventStore/pull/2496)
- Correct the Java package names in protocol buffers definitions [EventStore#2535](https://github.com/EventStore/EventStore/pull/2535)

### Added
- Jwt token support [EventStore#2510](https://github.com/EventStore/EventStore/pull/2510)
- An external gRPC endpoint for gossip [EventStore#2519](https://github.com/EventStore/EventStore/pull/2519)

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
