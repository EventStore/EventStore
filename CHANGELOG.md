# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]
_(none)_

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
