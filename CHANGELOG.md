# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed
- Improved synchronization during TFChunk disposal [EventStore#3674](https://github.com/EventStore/EventStore/pull/3674)
- Fix projection progress report. [EventStore#3655](https://github.com/EventStore/EventStore/pull/3655)
- #3697 FilteredAllSubscription checkpoint now continues to update after becomming live [EventStore#3726](https://github.com/EventStore/EventStore/pull/3726)
- (EventStore.TestClient) too strict cleanup condition causing a memory leak in rare cases when server becomes unresponsive. [EventStore#3705](https://github.com/EventStore/EventStore/pull/3705)
- (EventStore.TestClient) Cancelation of current command. [EventStore#3705](https://github.com/EventStore/EventStore/pull/3705)
- Use a separate IODispatcher for scavenge log [EventStore#3741](https://github.com/EventStore/EventStore/pull/3741)
- #3486  [EventStore#3752](https://github.com/EventStore/EventStore/pull/3752)
- Don't require an equals sign when parsing a command line argument followed by an integer [EventStore#3757](https://github.com/EventStore/EventStore/pull/3757)
- Ignore exceptions when failing to change PTable index file permissions [EventStore#3807](https://github.com/EventStore/EventStore/pull/3807)
- #2187  [EventStore#3816](https://github.com/EventStore/EventStore/pull/3816)
- Handle events that have been deleted from (now empty) chunks but not from the index [EventStore#3813](https://github.com/EventStore/EventStore/pull/3813)
- Compile error caused by conflicting PRs [EventStore#3822](https://github.com/EventStore/EventStore/pull/3822)
- Compile error caused by conflicting PRs [EventStore#3822](https://github.com/EventStore/EventStore/pull/3822)
- #3791  [EventStore#3814](https://github.com/EventStore/EventStore/pull/3814)
- race condition in ManagedProjection code when deleting a projection [EventStore#3812](https://github.com/EventStore/EventStore/pull/3812)
- projection code used to throw error if projection substreams (e.g. emitted streams, checkpoint streams etc.) did not exist, when deleting projection [EventStore#3812](https://github.com/EventStore/EventStore/pull/3812)
- Only log that a connection to a persistent subscription has been dropped if there was a subscription running on the TCP connection [EventStore#3824](https://github.com/EventStore/EventStore/pull/3824)
- Do not change the handlerType of a projection when it is updated [EventStore#3823](https://github.com/EventStore/EventStore/pull/3823)
- When multiple projection write requests arrive within short-period (create-create, create-delete, delete-create, delete-delete), projection manager used to pick same expected version number for both write requests which caused WrongExpectedVersion error for one of the request [EventStore#3817](https://github.com/EventStore/EventStore/pull/3817)
- add an error message when user performs an CRUD operation to $all stream using HTTP API [EventStore#3830](https://github.com/EventStore/EventStore/pull/3830)
- Log more readable error message in the server logs when the SSL handshakes fail. [EventStore#3832](https://github.com/EventStore/EventStore/pull/3832)
- Update log level to Warning if allowUnknownOptions sets to true [EventStore#3848](https://github.com/EventStore/EventStore/pull/3848)
- Remove options dump from server logs incase the config is incorrect [EventStore#3847](https://github.com/EventStore/EventStore/pull/3847)
- Only call `context.Success()` once to prevent early exit of command. [EventStore#3828](https://github.com/EventStore/EventStore/pull/3828)
- Improved log message for connectivity problem between nodes [EventStore#3839](https://github.com/EventStore/EventStore/pull/3839)
- Prevent risk of implicit transactions being partially written when crossing a chunk boundary on the leader node [EventStore#3808](https://github.com/EventStore/EventStore/pull/3808)
- Lower the log level of NACK logs for persistent subscriptions [EventStore#3854](https://github.com/EventStore/EventStore/pull/3854)
- Rename ubuntu build workflow with correct version [EventStore#3863](https://github.com/EventStore/EventStore/pull/3863)
- Want to remove PrepareCount and CommitCount db settings and docs [EventStore#3858](https://github.com/EventStore/EventStore/pull/3858)
- Bump reference of System.Security.Cryptography.Pkcs for CVE-2023-29331 [EventStore#3878](https://github.com/EventStore/EventStore/pull/3878)
- Bump transitive reference of System.Security.Cryptography.Pkcs from 7.0.2. [EventStore#3877](https://github.com/EventStore/EventStore/pull/3877)
- Bump `System.Security.Cryptography.Pkcs` from 7.0.1 to 7.0.2. [EventStore#3874](https://github.com/EventStore/EventStore/pull/3874)
- Running EventStore with `--dev` on Windows [EventStore#3875](https://github.com/EventStore/EventStore/pull/3875)
- Support bloom filters for 400gb+ index files [EventStore#3876](https://github.com/EventStore/EventStore/pull/3876)

### Added
- Source generator for dynamic message type ids [EventStore#3684](https://github.com/EventStore/EventStore/pull/3684)
- checkpoints metric [EventStore#3685](https://github.com/EventStore/EventStore/pull/3685)
- metric for tracking the current state of the Node [EventStore#3686](https://github.com/EventStore/EventStore/pull/3686)
- metric for tracking the current state of the Scavenge [EventStore#3686](https://github.com/EventStore/EventStore/pull/3686)
- metric for tracking the current state of the Index operations (merge/scavenge) [EventStore#3686](https://github.com/EventStore/EventStore/pull/3686)
- Histograms for gRPC reads and appends [EventStore#3695](https://github.com/EventStore/EventStore/pull/3695)
- a process-wide stopwatch for measuring durations [EventStore#3703](https://github.com/EventStore/EventStore/pull/3703)
- log errors for gRPC write flood. [EventStore#3705](https://github.com/EventStore/EventStore/pull/3705)
- Implement Init/Rebuild index status report. [EventStore#3707](https://github.com/EventStore/EventStore/pull/3707)
- Max queue durations over period per queue  [EventStore#3698](https://github.com/EventStore/EventStore/pull/3698)
- Queue Processing Duration Histograms by Message Type [EventStore#3730](https://github.com/EventStore/EventStore/pull/3730)
- Improved warning when using an invalid delimiter in gossip seed [EventStore#3757](https://github.com/EventStore/EventStore/pull/3757)
- Deprecation warnings for UnsafeIgnoreHardDeletes and AlwaysKeepsScavenged [EventStore#3778](https://github.com/EventStore/EventStore/pull/3778)
- The ability to set default admin and ops passwords on first run of the database [EventStore#3738](https://github.com/EventStore/EventStore/pull/3738)
- Suggestions for unknown/invalid configuration parameters (#3784) [EventStore#3785](https://github.com/EventStore/EventStore/pull/3785)
- Inauguration status tracker [EventStore#3699](https://github.com/EventStore/EventStore/pull/3699)
- Support for unix sockets [EventStore#3713](https://github.com/EventStore/EventStore/pull/3713)
- APIs for external redaction [EventStore#3713](https://github.com/EventStore/EventStore/pull/3713)
- default location for trusted root certs [EventStore#3811](https://github.com/EventStore/EventStore/pull/3811)
- ES version in gossip message between nodes [EventStore#3792](https://github.com/EventStore/EventStore/pull/3792)
- Every node will monitor versions of other alive nodes; if version mismatch detected in cluster, nodes will log this [EventStore#3792](https://github.com/EventStore/EventStore/pull/3792)
- "/gossip" endpoint will also have ES version info  [EventStore#3792](https://github.com/EventStore/EventStore/pull/3792)
- Handle redacted events in AtomPub [EventStore#3793](https://github.com/EventStore/EventStore/pull/3793)
- Options to restrict EventStoreDB access for anonymous users [EventStore#3787](https://github.com/EventStore/EventStore/pull/3787)
- Metrics for count of events being read/written and bytes being read [EventStore#3737](https://github.com/EventStore/EventStore/pull/3737)
- metrics for current/total/failed grpc calls [EventStore#3825](https://github.com/EventStore/EventStore/pull/3825)
- log of telemetry configuration on startup [EventStore#3835](https://github.com/EventStore/EventStore/pull/3835)
- Chunk and StreamInfo cache hits/misses metrics [EventStore#3829](https://github.com/EventStore/EventStore/pull/3829)
- Storage writer flush size/duration metrics [EventStore#3827](https://github.com/EventStore/EventStore/pull/3827)
- support for per projection execution timeout [EventStore#3831](https://github.com/EventStore/EventStore/pull/3831)
- Add system, process, and connection metrics. [EventStore#3777](https://github.com/EventStore/EventStore/pull/3777)
- Add paged bytes, virtual bytes, and thread pool tasks queue length metrics [EventStore#3838](https://github.com/EventStore/EventStore/pull/3838)
- context when logging the telemetry config [EventStore#3845](https://github.com/EventStore/EventStore/pull/3845)
- collect count, size and capacity metrics from DynamicCacheManager [EventStore#3840](https://github.com/EventStore/EventStore/pull/3840)
- metric for queue busy/idle [EventStore#3841](https://github.com/EventStore/EventStore/pull/3841)
- Add CG max Execution Engine Suspension duration. [EventStore#3842](https://github.com/EventStore/EventStore/pull/3842)
- Read $all command `RDALLGRPC`. [EventStore#3828](https://github.com/EventStore/EventStore/pull/3828)
- logging es version every 12 hours so that es version is logged in every log file [EventStore#3853](https://github.com/EventStore/EventStore/pull/3853)
- Based on header of the private key file, ES will now accept encrypted and unencrypted PKCS8 private key files [EventStore#3851](https://github.com/EventStore/EventStore/pull/3851)
- Support for the FIPS commercial plugin [EventStore#3846](https://github.com/EventStore/EventStore/pull/3846)
- polishing new metrics [EventStore#3879](https://github.com/EventStore/EventStore/pull/3879)
- Show MiniNode logs in case test fails [EventStore#3866](https://github.com/EventStore/EventStore/pull/3866)

### Changed
- Log warnings and errors when close to the max chunk number limit [EventStore#3643](https://github.com/EventStore/EventStore/pull/3643)
- Update UI build after latest changes. [EventStore#3683](https://github.com/EventStore/EventStore/pull/3683)
- Publish messages from the persistent subscriptions IODispatcher to the Persistent Subscriptions queue rather than the main queue [EventStore#3702](https://github.com/EventStore/EventStore/pull/3702)
- CI Unit test settings [EventStore#3712](https://github.com/EventStore/EventStore/pull/3712)
- log available commands separately. [EventStore#3705](https://github.com/EventStore/EventStore/pull/3705)
- Adjustments to reduce allocations [EventStore#3731](https://github.com/EventStore/EventStore/pull/3731)
- Support specifying values with a metric prefix with the `testclient`. [EventStore#3748](https://github.com/EventStore/EventStore/pull/3748)
- Jint library version to 3.0.0-beta-2048 [EventStore#3788](https://github.com/EventStore/EventStore/pull/3788)
- Minor adjustments in solution file [EventStore#3798](https://github.com/EventStore/EventStore/pull/3798)
- Move metric configuration from arrays to dictionaries. [EventStore#3837](https://github.com/EventStore/EventStore/pull/3837)
- IO metrics using Counter instrument to ObservableCounter instrument [EventStore#3834](https://github.com/EventStore/EventStore/pull/3834)
- Use `$GITHUB_OUTPUT` for workflow output. [EventStore#3833](https://github.com/EventStore/EventStore/pull/3833)
- Use latest `actions/checkout@v3`. [EventStore#3833](https://github.com/EventStore/EventStore/pull/3833)
- Disable queue processing metrics by default, they add a lot of histograms [EventStore#3850](https://github.com/EventStore/EventStore/pull/3850)

### Removed
- Unnecessary allocation on read [EventStore#3691](https://github.com/EventStore/EventStore/pull/3691)
- some redundant code [EventStore#3709](https://github.com/EventStore/EventStore/pull/3709)
- Cleanup V8 scripts. [EventStore#3740](https://github.com/EventStore/EventStore/pull/3740)
- Some logging to prevent flooding of console/logs. [EventStore#3828](https://github.com/EventStore/EventStore/pull/3828)

### Cherry picked from https
- //github.com/thefringeninja/EventStore/pull/3747 [EventStore#3755](https://github.com/EventStore/EventStore/pull/3755)
- //github.com/EventStore/EventStore/pull/3758 [EventStore#3776](https://github.com/EventStore/EventStore/pull/3776)
- //github.com/EventStore/EventStore/pull/3759 [EventStore#3773](https://github.com/EventStore/EventStore/pull/3773)
- //github.com/EventStore/EventStore/pull/3760 [EventStore#3772](https://github.com/EventStore/EventStore/pull/3772)
- //github.com/EventStore/EventStore/pull/3762 [EventStore#3770](https://github.com/EventStore/EventStore/pull/3770)
- //github.com/thefringeninja/EventStore/pull/3747 [EventStore#3754](https://github.com/EventStore/EventStore/pull/3754)
- //github.com/EventStore/EventStore/pull/3762 [EventStore#3771](https://github.com/EventStore/EventStore/pull/3771)
- //github.com/EventStore/EventStore/pull/3758 [EventStore#3775](https://github.com/EventStore/EventStore/pull/3775)
- //github.com/EventStore/EventStore/pull/3763 [EventStore#3768](https://github.com/EventStore/EventStore/pull/3768)
- //github.com/EventStore/EventStore/pull/3762 [EventStore#3769](https://github.com/EventStore/EventStore/pull/3769)
- //github.com/EventStore/EventStore/pull/3764 [EventStore#3774](https://github.com/EventStore/EventStore/pull/3774)
- //github.com/EventStore/EventStore/pull/3778 [EventStore#3779](https://github.com/EventStore/EventStore/pull/3779)
- //github.com/EventStore/EventStore/pull/3801 [EventStore#3806](https://github.com/EventStore/EventStore/pull/3806)
- //github.com/EventStore/EventStore/pull/3813 [EventStore#3821](https://github.com/EventStore/EventStore/pull/3821)
- //github.com/EventStore/EventStore/pull/3813 [EventStore#3821](https://github.com/EventStore/EventStore/pull/3821)
- //github.com/EventStore/EventStore/pull/3876 [EventStore#3880](https://github.com/EventStore/EventStore/pull/3880)
- //github.com/EventStore/EventStore/pull/3876 [EventStore#3881](https://github.com/EventStore/EventStore/pull/3881)

## [22.10.2] - 2023-05-15

### Fixed
- Handle events that have been deleted from (now empty) chunks but not from the index [EventStore#3813](https://github.com/EventStore/EventStore/pull/3813)
- Sender of create / update Persistent Subscription now notified of failure [EventStore#3747](https://github.com/EventStore/EventStore/pull/3747)

### Added
- Deprecation warnings for UnsafeIgnoreHardDeletes and AlwaysKeepsScavenged [EventStore#3778](https://github.com/EventStore/EventStore/pull/3778)

## [22.10.1] - 2023-02-13

### Fixed
- Whitespace [EventStore#3649](https://github.com/EventStore/EventStore/pull/3649)
- Patch Newtonsoft from `13.0.1` to `13.0.2` [EventStore#3677](https://github.com/EventStore/EventStore/pull/3677)
- Database checkpoints becomes inconsistent when running out of disk space [EventStore#3681](https://github.com/EventStore/EventStore/pull/3681)
- Slow persistent subscription consumer no longer slows down other subscribers [EventStore#3701](https://github.com/EventStore/EventStore/pull/3701)
- Cancel reads already in the reader queues when the gRPC call is cancelled [EventStore#3718](https://github.com/EventStore/EventStore/pull/3718)
- FilteredAllSubscription checkpoint now continues to update after becomming live [EventStore#3276](https://github.com/EventStore/EventStore/pull/3726)
- Downgraded an error log message that is not really an error to debug level [EventStore#3727](https://github.com/EventStore/EventStore/pull/3727)

### Added
- Improvements to Scavenge HTTP API (query if scavenge is running, stop any running scavenge) [EventStore#3656](https://github.com/EventStore/EventStore/pull/3656)
- ScavengeId to log context [EventStore#3656](https://github.com/EventStore/EventStore/pull/3656)
- More information to SLOW QUEUE MSG logs for reads and writes [EventStore#3706](https://github.com/EventStore/EventStore/pull/3706)

### Changed
- Use version 22.10 in the docs [EventStore#3714](https://github.com/EventStore/EventStore/pull/3714)

### Fixes
- #https://github.com/EventStore/home/issues/884 [EventStore#3694](https://github.com/EventStore/EventStore/pull/3694)

## [21.10.9] - 2022-11-21

### Added

- Dev mode option to generate and trust dev certs for a secure single node on localhost. [EventStore#3606](https://github.com/EventStore/EventStore/pull/3606)
- AllowUnknownOptions option that allows EventStoreDB to start when unknown options are present (default: false) [EventStore#3653](https://github.com/EventStore/EventStore/pull/3653)

### Fixed
- Node not going into ready state due to synchronization bug in UserManagementService [EventStore#3599](https://github.com/EventStore/EventStore/pull/3599)
- Race condition when creating a persistent subscription to a stream at the same time as creating that stream. [EventStore#3601](https://github.com/EventStore/EventStore/pull/3601)
- Quick responses for authentication requests when the node is not ready [EventStore#3609](https://github.com/EventStore/EventStore/pull/3609)
- Edge cases in maxage read fast path. [EventStore#3646](https://github.com/EventStore/EventStore/pull/3646)
- Make MergeIndexes endpoint return proper result. Fixes issue #3573  [EventStore#3573](https://github.com/EventStore/EventStore/pull/3573)
- Incorrect error message when deleting a stream using gRPC. Fixes issue #3547 [EventStore#3583](https://github.com/EventStore/EventStore/pull/3583)
- Duplicate Persistent Subscriptions in the EventStoreDB UI [EventStore#3533](https://github.com/EventStore/EventStore/pull/3533)

### Changed

- Start standard projections and atompub over http when running in dev mode [EventStore#3615](https://github.com/EventStore/EventStore/pull/3615)
- Display node status as 'Unreachable' instead of 'Dead' in the cluster status page [EventStore#3612](https://github.com/EventStore/EventStore/pull/3612)

## [22.10.0] - 2022-11-10

### Security
- Updated Newtonsoft.Json from 11.0.2 to 13.0.1. [EventStore#3507](https://github.com/EventStore/EventStore/pull/3507)
- Upgrade sqlite to v3.39.2 for [CVE-2022-35737](https://github.com/advisories/GHSA-jw36-hf63-69r9) [EventStore#3631](https://github.com/EventStore/EventStore/pull/3631)

### Added
- New scavenge algorithm [EventStore#3520](https://github.com/EventStore/EventStore/pull/3520)
- Support for custom log templates [EventStore#3577](https://github.com/EventStore/EventStore/pull/3577)
- Support for logical chunk numbers up to 400k [EventStore#3589](https://github.com/EventStore/EventStore/pull/3589)
- Logging when server receives gRPC calls that are retries [EventStore#3588](https://github.com/EventStore/EventStore/pull/3588)
- CI: run tests in target runtime container [EventStore#3597](https://github.com/EventStore/EventStore/pull/3597)
- support for .NET 6.0 [EventStore#3595](https://github.com/EventStore/EventStore/pull/3595)
- Dynamically resize stream info caches based on available memory [EventStore#3569](https://github.com/EventStore/EventStore/pull/3569)
- Dev mode option to generate and trust dev certs for a secure single node on localhost. [EventStore#3606](https://github.com/EventStore/EventStore/pull/3606)
- Tuning new scavenge based on cloud tests [EventStore#3645](https://github.com/EventStore/EventStore/pull/3645)
- AllowUnknownOptions option that allows EventStoreDB to start when unknown options are present (default: false) [EventStore#3653](https://github.com/EventStore/EventStore/pull/3653)

### Fixed
- Make MergeIndexes endpoint return proper result. Fixes issue #3573  [EventStore#3573](https://github.com/EventStore/EventStore/pull/3573)
- gRPC calls can no longer stall if received during server initialization. [EventStore#3584](https://github.com/EventStore/EventStore/pull/3584)
- Incorrect error message when deleting a stream using gRPC. Fixes issue #3547 [EventStore#3583](https://github.com/EventStore/EventStore/pull/3583)
- Node not going into ready state due to synchronization bug in UserManagementService [EventStore#3599](https://github.com/EventStore/EventStore/pull/3599)
- Race condition when creating a persistent subscription to a stream at the same time as creating that stream. [EventStore#3601](https://github.com/EventStore/EventStore/pull/3601)
- Quick responses for authentication requests when the node is not ready [EventStore#3609](https://github.com/EventStore/EventStore/pull/3609)
- Exit application when hosted service shuts down [EventStore#3624](https://github.com/EventStore/EventStore/pull/3624)
- Markdown syntax error [EventStore#3629](https://github.com/EventStore/EventStore/pull/3629)
- Downgrade GitInfo due to breaking change [EventStore#3633](https://github.com/EventStore/EventStore/pull/3633)
- Duplicate Persistent Subscriptions in the EventStoreDB UI [EventStore#3533](https://github.com/EventStore/EventStore/pull/3533)
- Silenced warning `Tried to read actual position -1` when it does not represent a suspicious occurrence. [EventStore#3632](https://github.com/EventStore/EventStore/pull/3632)
- Edge cases in maxage read fast path. [EventStore#3646](https://github.com/EventStore/EventStore/pull/3646)

### Changed
- Update support for Keep Alive as per RFC. Fixes issue #3259  [EventStore#3574](https://github.com/EventStore/EventStore/pull/3574)
- Deleting persistent subscriptions to $all over HTTP is not supported [EventStore#3593](https://github.com/EventStore/EventStore/pull/3593)
- Don't catch and log `OperationCanceledException` in `PolicyAuthorizationProvider`. [EventStore#3594](https://github.com/EventStore/EventStore/pull/3594)
- Extend unit test timings slightly for tests that sometimes timeout [EventStore#3596](https://github.com/EventStore/EventStore/pull/3596)
- Start standard projections and atompub over http when running in dev mode [EventStore#3615](https://github.com/EventStore/EventStore/pull/3615)
- Display node status as 'Unreachable' instead of 'Dead' in the cluster status page [EventStore#3612](https://github.com/EventStore/EventStore/pull/3612)
- Fix typos and improve grammar on the server documentations' diagnostics page [EventStore#3616](https://github.com/EventStore/EventStore/pull/3616)
- Update EventStore.Plugins to latest version [EventStore#3619](https://github.com/EventStore/EventStore/pull/3619)

## [21.10.8] - 2022-09-16

### Security
- Updated Newtonsoft.Json from 11.0.2 to 13.0.1. [EventStore#3507](https://github.com/EventStore/EventStore/pull/3507)

### Added
- Support for custom log templates [EventStore#3577](https://github.com/EventStore/EventStore/pull/3577)
- Support for logical chunk numbers up to 400k [EventStore#3589](https://github.com/EventStore/EventStore/pull/3589)

### Fixed
- gRPC calls can no longer stall if received during server initialization. [EventStore#3584](https://github.com/EventStore/EventStore/pull/3584)
- Memory leak caused by EventPipeEventSource. [EventStore#3578](https://github.com/EventStore/EventStore/pull/3578) - Thanks [@PaskeS](https://github.com/PaskeS)!
- Double serialization of projections using $init function [EventStore#3564](https://github.com/EventStore/EventStore/pull/3564) - Thanks [@PaskeS](https://github.com/PaskeS)!

## [22.6.0] - 2022-07-21

### Changed
- Consolidate to single protobuf implementation [EventStore#3362](https://github.com/EventStore/EventStore/pull/3362)
- Updated gRPC to include fix for flaky tests [EventStore#3364](https://github.com/EventStore/EventStore/pull/3364)
- Reject gRPC call to create a projection if `TrackEmittedStreams=true` and `EmitEnabled=false` [EventStore#3412](https://github.com/EventStore/EventStore/pull/3412)
- Proto names of `emit_enabled` and `track_emitted_streams` swapped for backwards compatibility [EventStore#3412](https://github.com/EventStore/EventStore/pull/3412)
- Read operations in IODispatcher are no longer tracked if they don't have a timeoutAction [EventStore#3435](https://github.com/EventStore/EventStore/pull/3435)
- Sort the dump of the configuration settings alphabetically [EventStore#3475](https://github.com/EventStore/EventStore/pull/3475)
- Update UI for 22.6.0 [EventStore#3519](https://github.com/EventStore/EventStore/pull/3519)

### Fixed
- Update the epoch checkpoint with the proper position before going offline for truncation [EventStore#3414](https://github.com/EventStore/EventStore/pull/3414)
- Removed aggregate exception when closing a call to the stats endpoint [EventStore#3495](https://github.com/EventStore/EventStore/pull/3495)
- Possiblility of unreplicated writes appearing in reads and subscriptions [EventStore#3527](https://github.com/EventStore/EventStore/pull/3527)
- Removed unnecessary "Should never complete request twice error" [EventStore#3522](https://github.com/EventStore/EventStore/pull/3522)

### Added
- Logging around read timeouts in projection emitted streams and emitted stream trackers [EventStore#3435](https://github.com/EventStore/EventStore/pull/3435)
- Populate $all position for stream reads/subscriptions/persistent subscriptions. gRPC only. Non-transaction events only. [EventStore#3459](https://github.com/EventStore/EventStore/pull/3459)
- `TrustedRootCertificateStoreName`, `TrustedRootCertificateStoreLocation`, `TrustedRootCertificateSubjectName` and `TrustedRootCertificateThumbprint` certificate options. [EventStore#3498](https://github.com/EventStore/EventStore/pull/3498)
- Allow loading trusted root certificates from the Windows cert store. [EventStore#3498](https://github.com/EventStore/EventStore/pull/3498)
- Better summaries for CI Runs [EventStore#3496](https://github.com/EventStore/EventStore/pull/3496) - Thanks [@Tyrrrz](https://github.com/Tyrrrz)!

## [21.10.7] - 2022-07-29

## Fixed

- Prevent gRPC subscriptions from hanging while catching up [EventStore#3532](https://github.com/EventStore/EventStore/pull/3532)
- Fix "Known" and "Current" values showing as NaN in Persistent Subscriptions Commercial UI

## [21.10.6] - 2022-07-20

### Fixed
- Possiblility of unreplicated writes appearing in reads and subscriptions [EventStore#3527](https://github.com/EventStore/EventStore/pull/3527)
- Removed unnecessary "Should never complete request twice error" [EventStore#3522](https://github.com/EventStore/EventStore/pull/3522)

## [21.10.5] - 2022-06-15

This changelog includes the fixes from 21.10.3 and 21.10.4, as these were cloud-only releases.

### Added

- Support for DNS discovery with a secure cluster having only DNS SANs (including wildcard SANs). [EventStore#3460](https://github.com/EventStore/EventStore/pull/3460)
- `deadline_duration` feature for batch append. [EventStore#3454](https://github.com/EventStore/EventStore/pull/3454)
- Fast path for forward reads that start after the LastEventNumber. [EventStore#3479](https://github.com/EventStore/EventStore/pull/3479)

### Fixed

- Attempt to reconnect to the leader every second if the node fails to establish a connection (for example due to DNS lookup timeout). [EventStore#3462](https://github.com/EventStore/EventStore/pull/3462)
- Fix invalid cast when completing a scavenge after it has been interrupted. [EventStore#3478](https://github.com/EventStore/EventStore/pull/3478)
- Can now use the ptable bloom filters after an index scavenge. [EventStore#3493](https://github.com/EventStore/EventStore/pull/3493)
- Ensure no pending writes can be incorrectly acked or published when going offline for truncation. [EventStore#3502](https://github.com/EventStore/EventStore/pull/3502)
- Metadata values need to be raw json format. [EventStore#3503](https://github.com/EventStore/EventStore/pull/3503)
- Fix invalid event number error when opening a deleted event from $all in the UI. [EventStore.UI#326](https://github.com/EventStore/EventStore.UI/pull/326)
- Fix previous button showing an empty screen in the UI if there are no events. [EventStore.UI#323](https://github.com/EventStore/EventStore.UI/pull/323)
- Handle NotFound errors more gracefully in the UI. [EventStore.UI#324](https://github.com/EventStore/EventStore.UI/pull/324)
- Fix off by one error in behind message count in persistent subscriptions UI. [EventStore.UI#320](https://github.com/EventStore/EventStore.UI/pull/320)

### Changed

- Improve state serialization speed. [EventStore#3490](https://github.com/EventStore/EventStore/pull/3490)

### Removed

- Remove the red and green status dots from persistent subscriptions UI. [EventStore.UI#318](https://github.com/EventStore/EventStore.UI/pull/318)

## [21.10.4] - 2022-04-26 (Cloud only)

### Fixed
- Invalid cast when trying to complete a scavenge after the scavenge was interrupted [EventStore#3476](https://github.com/EventStore/EventStore/pull/3476)

### Added
- Fast path for forward reads that start after the LastEventNumber [EventStore#3474](https://github.com/EventStore/EventStore/pull/3474)

## [21.10.3] - 2022-04-11 (Cloud only)

### Fixed
- Attempt to reconnect to the leader every second if the node fails to establish a connection (for example due to DNS lookup timeout). [EventStore#3458](https://github.com/EventStore/EventStore/pull/3458)

### Added
- Support for DNS discovery with a secure cluster having only DNS SANs (including wildcard SANs) [EventStore#3427](https://github.com/EventStore/EventStore/pull/3427)
- deadline_duration feature for batch append [EventStore#3454](https://github.com/EventStore/EventStore/pull/3454)

## [21.10.2] - 2022-03-04

### Fixed
- Set chunk end number to the max between current end number and the added chunk number [EventStore#3365](https://github.com/EventStore/EventStore/pull/3365)
- Use the term "certificate signed by a private CA" instead of "self-signed certificates" [EventStore#3372](https://github.com/EventStore/EventStore/pull/3372)
- Directory.EnumerateFiles regression causing slower startup/truncation times on large databases [EventStore#3385](https://github.com/EventStore/EventStore/pull/3385)
- Include current stream revision on internal delete messages [EventStore#3405](https://github.com/EventStore/EventStore/pull/3405)
- Null reference exception when getting persistent subscription info [EventStore#3408](https://github.com/EventStore/EventStore/pull/3408)
- IODispatcher bug causing password change notifications to be missed sometimes [EventStore#3421](https://github.com/EventStore/EventStore/pull/3421)
- Make password changes more robust [EventStore#3429](https://github.com/EventStore/EventStore/pull/3429)
- Improved Stream Existence Filter flushes [EventStore#3425](https://github.com/EventStore/EventStore/pull/3425)
- Send full certificate chain from both server and client side during TLS handshake (requires manually adding intermediate certificates to the store) [EventStore#3446](https://github.com/EventStore/EventStore/pull/3446)
- Prevent projections subsystem from getting stuck in a stopping state due to read timeouts [EventStore#3441](https://github.com/EventStore/EventStore/pull/3441)
- Risk of PreLeader not successfully transitioning to Leader if a TCP disconnect occurs at just the right time. [EventStore#3443](https://github.com/EventStore/EventStore/pull/3443)

### Added
- Expose `emitEnabled` on creation of continuous projection. [EventStore#3384](https://github.com/EventStore/EventStore/pull/3384)
- Log GC settings on startup [EventStore#3434](https://github.com/EventStore/EventStore/pull/3434)

## [21.10.1] - 2021-12-16

### Fixed
- Exception in scheduled message callback crashes server [EventStore#3270](https://github.com/EventStore/EventStore/pull/3270)
- IODispatcher is now threadsafe for request tracking [EventStore#3270](https://github.com/EventStore/EventStore/pull/3270)
- InvalidOperationException caused by reading RequestStream after completing the PersistentSubscription gRPC call  [EventStore#3287](https://github.com/EventStore/EventStore/pull/3287)
- Return correct status code in batch append [EventStore#3295](https://github.com/EventStore/EventStore/pull/3295)
- Fix `partitionBy` not working with numbers [EventStore#3325](https://github.com/EventStore/EventStore/pull/3325)
- Fix link parsing in persistent subscription service. [EventStore#3328](https://github.com/EventStore/EventStore/pull/3328)
- MaxAge fast path: corner cases and support for SkipIndexScanOnRead [EventStore#3339](https://github.com/EventStore/EventStore/pull/3339)
- use last indexed position of all stream when consumer subscribes to all filtered live [EventStore#3342](https://github.com/EventStore/EventStore/pull/3342)
- Prevent risk of deadlock when creating a PersistentSubscriptionGroup [EventStore#3344](https://github.com/EventStore/EventStore/pull/3344)

### Added
- Support for ARM64 on Linux. [EventStore#3076](https://github.com/EventStore/EventStore/pull/3076)
- GetInfo, ReplayParked, List, RestartSubsystem operations to persistent subscription gRPC proto [EventStore#3352](https://github.com/EventStore/EventStore/pull/3352)
- String ConsumerStrategy property when creating persistent subscriptions over gRPC [EventStore#3352](https://github.com/EventStore/EventStore/pull/3352)
- Extra logging on startup [EventStore#3346](https://github.com/EventStore/EventStore/pull/3346)

### Changed
- Use file checkpoints on Linux and memory mapped checkpoints on Windows [EventStore#3340](https://github.com/EventStore/EventStore/pull/3340)
- Deprecate NamedConsumerStrategy when creating persistent subscriptions over gRPC [EventStore#3352](https://github.com/EventStore/EventStore/pull/3352)

## [20.10.5] - 2021-12-06

### Fixed

- Prevent nodes that aren't part of a cluster from pruning gossip seeds [EventStore#3116](https://github.com/EventStore/EventStore/pull/3116)
- Overflow error in persistent subscriptions extra statistics [EventStore#3162](https://github.com/EventStore/EventStore/pull/3162)
- Prevent the EpochManager from attempting to read epochs that should have been cached [EventStore#3189](https://github.com/EventStore/EventStore/pull/3189)
- Off-by-one error in Index committer service [EventStore#3196](https://github.com/EventStore/EventStore/pull/3196)
- Race condition where a node becomes leader and immediately writes to streams that have recently been written to but not[EventStore#3187](https://github.com/EventStore/EventStore/pull/3187)
- Additional Options to Logging [EventStore#3207](https://github.com/EventStore/EventStore/pull/3207)
- Object disposed exception in EventCountersHelper on shutdown [EventStore#3245](https://github.com/EventStore/EventStore/pull/3245)
- Added verification checking if a message is of NotHandled type in PersistentSubscriptions Read message handling [EventStore#3158](https://github.com/EventStore/EventStore/pull/3158)
- Breaking change in log file name by removing log rotation options [EventStore#3267](https://github.com/EventStore/EventStore/pull/3267)
- Exception in scheduled message callback crashes server [EventStore#3272](https://github.com/EventStore/EventStore/pull/3272)
- IODispatcher is now threadsafe for request tracking [EventStore#3272](https://github.com/EventStore/EventStore/pull/3272)
- Clear HeadingEventReader cache when stopping the readers [EventStore#3233](https://github.com/EventStore/EventStore/pull/3233)
- InvalidOperationException caused by reading RequestStream after completing the PersistentSubscription gRPC call [EventStore#3294](https://github.com/EventStore/EventStore/pull/3294)
- 404 error for scavenge no longer shows in UI [EventStore#3284](https://github.com/EventStore/EventStore/pull/3284)
- Replica stats show in the UI [EventStore#3284](https://github.com/EventStore/EventStore/pull/3284)
- Ensure the projections IODispatcher clears up pending requests [EventStore#3244](https://github.com/EventStore/EventStore/pull/3244)
- Use the right advertised host value in a cluster configuration [EventStore#3224](https://github.com/EventStore/EventStore/pull/3224)
- MaxAge fast path: corner cases and support for SkipIndexScanOnRead [EventStore#3330](https://github.com/EventStore/EventStore/pull/3330)
- Fix link parsing in persistent subscription service [EventStore#3328](https://github.com/EventStore/EventStore/pull/3328)
- Prevent risk of deadlock when creating a PersistentSubscriptionGroup [EventStore#3343](https://github.com/EventStore/EventStore/pull/3343)

### Changed
- Check /etc/eventstore First for Custom Configuration [EventStore#3207](https://github.com/EventStore/EventStore/pull/3207)
- Warn that projection debugging uses a different engine to the one running in Event Store [EventStore#3284](https://github.com/EventStore/EventStore/pull/3284)
- Do not download intermediate certificates from AIA URLs [EventStore#3283](https://github.com/EventStore/EventStore/pull/3283)
- Use file checkpoints on Linux and memory mapped checkpoints on Windows [EventStore#3332](https://github.com/EventStore/EventStore/pull/3332)

### Added
- LeaderElectionTimeoutMs option to allow configuring the timeout for election messages. [EventStore#3263](https://github.com/EventStore/EventStore/pull/3263)
- Support for Intermediate CA certificates [EventStore#3283](https://github.com/EventStore/EventStore/pull/3283)
- Validation of certificate chain with node's own certificate on start up [EventStore#3283](https://github.com/EventStore/EventStore/pull/3283)

## [21.10.0] - 2021-11-03

### Fixed
- HTTP port parameter in docker-compose.yaml  [EventStore#2995](https://github.com/EventStore/EventStore/pull/2995)
- WrongExpectedVersion when deleting a projection with all delete options  [EventStore#3014](https://github.com/EventStore/EventStore/pull/3014)
- Prevent emitted checkpoint streams from being deleted when not enabled. [EventStore#3030](https://github.com/EventStore/EventStore/pull/3030)
- Http authentication when a `:` is present in the user's password [EventStore#3070](https://github.com/EventStore/EventStore/pull/3070)
- Persistent subscriptions: Rename LastProcessedEventPosition to LastCheckpointedEventPosition [EventStore#3073](https://github.com/EventStore/EventStore/pull/3073)
- Potential race condition in BatchAppend [EventStore#3138](https://github.com/EventStore/EventStore/pull/3138)
- Added verification checking if a message is of NotHandled type in PersistentSubscriptions Read message handling [EventStore#3158](https://github.com/EventStore/EventStore/pull/3158)
- Issue with not handling null event data correctly in Projections [EventStore#3171](https://github.com/EventStore/EventStore/pull/3171)
- Incorrectly setting OutputState on IQuerySources when only a fold is defined in Projections [EventStore#3171](https://github.com/EventStore/EventStore/pull/3171)
- Off-by-one error in Index committer service [EventStore#3186](https://github.com/EventStore/EventStore/pull/3186)
- Node stays stuck in Leader state until next elections if a quorum never emerges [EventStore#3181](https://github.com/EventStore/EventStore/pull/3181)
- User provided metadata to be formatted correctly in Projections [EventStore#3188](https://github.com/EventStore/EventStore/pull/3188)
- Incorrect configuration file selection on linux [EventStore#3159](https://github.com/EventStore/EventStore/pull/3159)
- Prevent the EpochManager from attempting to read epochs that should have been cached [EventStore#3198](https://github.com/EventStore/EventStore/pull/3198)
- Race condition where a node becomes leader and immediately writes to streams that have recently been written to but not indexed, resulting in events with incorrect numbers in their streams. [EventStore#3201](https://github.com/EventStore/EventStore/pull/3201)
- Handling $deleted in the interpreted projections runtime. [EventStore#3216](https://github.com/EventStore/EventStore/pull/3216)
- Incorrect log message templates [EventStore#3223](https://github.com/EventStore/EventStore/pull/3223)
- LinkTo metadata not properly read in Projections [EventStore#3227](https://github.com/EventStore/EventStore/pull/3227)
- Clear HeadingEventReader cache in Projections when stopping the readers [EventStore#3233](https://github.com/EventStore/EventStore/pull/3233)
- Use the right advertised host value in a cluster configuration [EventStore#3224](https://github.com/EventStore/EventStore/pull/3224)
- Ensure the IODispatcher clears up pending requests [EventStore#3244](https://github.com/EventStore/EventStore/pull/3244)
- Replica stats page correctly shows catching up nodes [EventStore#3247](https://github.com/EventStore/EventStore/pull/3247)
- Set last checkpoint for persistent subscriptions when they are loaded [EventStore#3241](https://github.com/EventStore/EventStore/pull/3241)
- Object disposed exception in `EventCountersHelper` on shutdown [EventStore#3242](https://github.com/EventStore/EventStore/pull/3242)
- Memory Leak in Batch Append [EventStore#3253](https://github.com/EventStore/EventStore/pull/3253)
- Batch append not using requested uuid format [EventStore#3255](https://github.com/EventStore/EventStore/pull/3255)
- StreamExistenceFilter truncation corner case [EventStore#3257](https://github.com/EventStore/EventStore/pull/3257)

### Added
- Alpine Docker Image [EventStore#3069](https://github.com/EventStore/EventStore/pull/3069)
- A bloom filter to quickly check stream existence [EventStore#3078](https://github.com/EventStore/EventStore/pull/3078)
- LeaderElectionTimeoutMs option to allow configuring the timeout for election messages [EventStore#3121](https://github.com/EventStore/EventStore/pull/3121)
- Add a log message describing how to disable the stream existence filter [EventStore#3172](https://github.com/EventStore/EventStore/pull/3172)
- New options to control logging: --log-console-format, --log-file-size, --log-file-interval, --log-file-retention-count, and --disable-log-file [EventStore#3159](https://github.com/EventStore/EventStore/pull/3159)
- Event type index for LogV3 [EventStore#3114](https://github.com/EventStore/EventStore/pull/3114)
- ClientCapabilities proto for discovering which gRPC methods are available on the server [EventStore#3194](https://github.com/EventStore/EventStore/pull/3194)
- ServerFeatures proto to allow clients to discover which features are available on the server [EventStore#3251](https://github.com/EventStore/EventStore/pull/3251)
- Control message to indicate client compatibility level on reads [EventStore#3197](https://github.com/EventStore/EventStore/pull/3197)
- Bloom Filters and LRU Caches to PTables [EventStore#3161](https://github.com/EventStore/EventStore/pull/3161)
- Log level to compact json log output [EventStore#3212](https://github.com/EventStore/EventStore/pull/3212)
- Support for Intermediate CA certificates [EventStore#3176](https://github.com/EventStore/EventStore/pull/3176)
- Validation of certificate chain with node's own certificate on start up [EventStore#3176](https://github.com/EventStore/EventStore/pull/3176)

### Changed
- Added a message to timeout error on append / delete [EventStore#3054](https://github.com/EventStore/EventStore/pull/3054)
- Do not download intermediate certificates from AIA URLs [EventStore#3176](https://github.com/EventStore/EventStore/pull/3176)
- Improve stream existence bloom filter V2 initialization [EventStore#3234](https://github.com/EventStore/EventStore/pull/3234)

### Removed
- V8 projection runtime [EventStore#3193](https://github.com/EventStore/EventStore/pull/3193)

## [20.10.4] - 2021-07-22

### Added

- Configure kestrel with kestrelsettings.json [EventStore#3039](https://github.com/EventStore/EventStore/pull/3039)
- Make logconfig.json location fully configurable [EventStore#3053](https://github.com/EventStore/EventStore/pull/3053)
- Add Commit Hash to .NET Version Info At Startup [EventStore#3060](https://github.com/EventStore/EventStore/pull/3060)

### Fixed

- Improve lookup of first non-expired events in long stream with maxage [EventStore#3046](https://github.com/EventStore/EventStore/pull/3046)
- Aborted http requests are no longer logged in the authentication middleware [EventStore#3044](https://github.com/EventStore/EventStore/pull/3056)
- Fix projections getting stuck when reading from truncated streams [EventStore#3056](https://github.com/EventStore/EventStore/pull/3056)
- Prevent scavenged events from being passed to ExecuteHandler [EventStore#3055](https://github.com/EventStore/EventStore/pull/3055)

## [21.6.0] - 2021-06-24

### Added
- LogV3 abstraction points [EventStore#2907](https://github.com/EventStore/EventStore/pull/2907)
- V3 Epoch Raw Record [EventStore#2908](https://github.com/EventStore/EventStore/pull/2908)
- LogV3 PartitionType and StreamType structs and creation methods [EventStore#2918](https://github.com/EventStore/EventStore/pull/2918)
- V3 Epoch integration [EventStore#2911](https://github.com/EventStore/EventStore/pull/2911)
- EventId is now passed into projections [EventStore#2928](https://github.com/EventStore/EventStore/pull/2928)
- ISystemStreamLookup abstraction point for LogV3 [EventStore#2923](https://github.com/EventStore/EventStore/pull/2923)
- Persistent subscriptions to $all for gRPC clients [EventStore#2869](https://github.com/EventStore/EventStore/pull/2869)
- LogV3 EventType, ContentType & Partition structs and creation methods [EventStore#2931](https://github.com/EventStore/EventStore/pull/2931)
- Simple stream writes for LogV3 [EventStore#2930](https://github.com/EventStore/EventStore/pull/2930)
- TransactionStart and TransactionEnd structs for LogV3. [EventStore#2953](https://github.com/EventStore/EventStore/pull/2953)
- Implement Monitoring gRPC API. [EventStore#2932](https://github.com/EventStore/EventStore/pull/2932)
- Add the ability to configure kestrel with kestrelsettings.json [EventStore#2949](https://github.com/EventStore/EventStore/pull/2949)
- Option to switch between v2 & v3 log format [EventStore#2972](https://github.com/EventStore/EventStore/pull/2972)
- LogV3 Stream Records and Stream Name Index [EventStore#2959](https://github.com/EventStore/EventStore/pull/2959)
- Faster seek for first non-expired events in long streams with $max-age set  [EventStore#2981](https://github.com/EventStore/EventStore/pull/2981)
- auto configuration for stream cache, reader threads and worker threads. [EventStore#2902](https://github.com/EventStore/EventStore/pull/2902)
- Interpreter runtime for user projections [EventStore#2951](https://github.com/EventStore/EventStore/pull/2951)
- Options to switch user runtime back to legacy v8 [EventStore#2951](https://github.com/EventStore/EventStore/pull/2951)
- Initial creation of the LogV3 root partition. [EventStore#2982](https://github.com/EventStore/EventStore/pull/2982)
- NFIBrokerage/spear as a community gRPC client for Elixir [EventStore#2939](https://github.com/EventStore/EventStore/pull/2939)
- Make Log Configuration Path Configurable [EventStore#3002](https://github.com/EventStore/EventStore/pull/3002)

### Fixed
- Regression in TCP connection [EventStore#2834](https://github.com/EventStore/EventStore/pull/2834)
- Mutex being released on wrong thread resulting in an annoying log message on shutdown [EventStore#2838](https://github.com/EventStore/EventStore/pull/2838)
- Keep alive timeout check [EventStore#2861](https://github.com/EventStore/EventStore/pull/2861)
- TestClient not exiting after executing `--command` [EventStore#2871](https://github.com/EventStore/EventStore/pull/2871)
- Rdall for TestClient [EventStore#2892](https://github.com/EventStore/EventStore/pull/2892)
- Parsing of yaml config options specified as an array [EventStore#2906](https://github.com/EventStore/EventStore/pull/2906)
- Start projections when requested [EventStore#2929](https://github.com/EventStore/EventStore/pull/2929)
- Handle missing case for UpdatePersistentSubscriptionTo{Stream,All}Result.DoesNotExist [EventStore#2941](https://github.com/EventStore/EventStore/pull/2941)
- In gRPC projection management, disable a projection when writing a checkpoint, and abort it if not writing a checkpoint. [EventStore#2944](https://github.com/EventStore/EventStore/pull/2944)
- Parameter count mismatch when loading the dashboard in the UI [EventStore#2964](https://github.com/EventStore/EventStore/pull/2964)
- Tests failing with empty error message in `EventStore.Core.Tests.Http.Cluster.when_requesting_from_follower.*`. [EventStore#2969](https://github.com/EventStore/EventStore/pull/2969)
- Tests failing with `already exists` error because same initial values were being re-used in `EventStore.Core.Tests.ClientAPI.when_connection_drops_messages_that_have_run_out_of_retries_are_not_retried`. [EventStore#2969](https://github.com/EventStore/EventStore/pull/2969)
- Fix projections getting stuck when reading from truncated streams [EventStore#2979](https://github.com/EventStore/EventStore/pull/2979)
- Only return nodes in Follower state in tests. [EventStore#2974](https://github.com/EventStore/EventStore/pull/2974)
- Wait for node to become a leader/follower in tests. [EventStore#2974](https://github.com/EventStore/EventStore/pull/2974)
- Fix --version printing [EventStore#3004](https://github.com/EventStore/EventStore/pull/3004)
- Aborted http requests are no longer logged in the authentication middleware [EventStore#3006](https://github.com/EventStore/EventStore/pull/3006)
- Prevent scavenged events from being passed to Projections [EventStore#2966](https://github.com/EventStore/EventStore/pull/2966)
- Fix Potential Server Side Crash w/ gRPC Batch Appends [EventStore#2991](https://github.com/EventStore/EventStore/pull/2991)

### Changed
- Make Microsoft.NETFramework.ReferenceAssemblies reference private [EventStore#2859](https://github.com/EventStore/EventStore/pull/2859)
- Internal configuration system now based on `Microsoft.Extensions.Configuration` [EventStore#2833](https://github.com/EventStore/EventStore/pull/2833)
- TCP client moved from main repo to https://github.com/EventStore/EventStoreDB-Client-Dotnet-Legacy [EventStore#2863](https://github.com/EventStore/EventStore/pull/2863)
- Generalized TF and Index in preparation for LogV3 [EventStore#2889](https://github.com/EventStore/EventStore/pull/2889)
- Change the user projection runtime to use an interpreter rather than v8 [EventStore#2951](https://github.com/EventStore/EventStore/pull/2951)
- Changed Windows .dotnet prerequisite to https and changed build command [EventStore#2877](https://github.com/EventStore/EventStore/pull/2877)
- Custom kestrel default settings [EventStore#2984](https://github.com/EventStore/EventStore/pull/2984)
- Visibility and gRPC generation changes to better support testing without needing clients to be referenced [EventStore#2942](https://github.com/EventStore/EventStore/pull/2942)
- Merge sequential checks in && or || expressions [EventStore#2961](https://github.com/EventStore/EventStore/pull/2961)
- Assorted minor adjustments to V3 schema following discussions [EventStore#2958](https://github.com/EventStore/EventStore/pull/2958)
- Test names to fit the existing pattern [EventStore#2978](https://github.com/EventStore/EventStore/pull/2978)
- V3 StreamNumbers are now 32bit instead of 64bit [EventStore#2976](https://github.com/EventStore/EventStore/pull/2976)
- Allow specifying a filter when creating a persistent subscription to $all [EventStore#2970](https://github.com/EventStore/EventStore/pull/2970)

## [20.10.3] Server - 2020-04-14

### Added
- --stream-info-cache-capacity option to allow setting the cache capacity of the ReadIndex. [EventStore#2762](https://github.com/EventStore/EventStore/pull/2762)
- auto configuration for stream cache, reader threads and worker threads. [EventStore#2934](https://github.com/EventStore/EventStore/pull/2934)

### Changed
- gRPC settings [EventStore#2934](https://github.com/EventStore/EventStore/pull/2934)

## [20.10.2] Server - 2020-03-12

### Changed
- Increased the maximum chunk count to patch issue with 25 logical TB. [EventStore#2830](https://github.com/EventStore/EventStore/pull/2830)
- Updated internal dependencies and added client builds for .NET 5.0 [EventStore#2764](https://github.com/EventStore/EventStore/pull/2764)

### Fixed
- Permission Denied when performing privileged commands on a follower [EventStore#2803](https://github.com/EventStore/EventStore/pull/2803)
- --insecure has stopped working after targeting .NET 5.0 [EventStore#2779](https://github.com/EventStore/EventStore/pull/2779)
- Track retry count for persistent subscription messages after a client has lost connection. [EventStore#2797](https://github.com/EventStore/EventStore/pull/2797)
- Time out gossip discovery on the TCP client if the task does not complete [EventStore#2821](https://github.com/EventStore/EventStore/pull/2821)
- Check for old/replayed events only if the event passes the event filter in projections [EventStore#2809](https://github.com/EventStore/EventStore/pull/2809)
- Prevent a projection checkpoint from being emitted at same position twice [EventStore#2824](https://github.com/EventStore/EventStore/pull/2824)
- Proactively send heartbeat requests to the remote party if no data was sent within the last heartbeat interval [EventStore#2772](https://github.com/EventStore/EventStore/pull/2772)
- Regression in TCP connection introduced by commit: cd2aa67 from PR: #2772 [EventStore#2834][https://github.com/EventStore/EventStore/pull/2834]

### Added
- Content Type Validation to projections which will allow projections to only handle valid json events if isJson is set to true [EventStore#2812](https://github.com/EventStore/EventStore/pull/2812)

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
