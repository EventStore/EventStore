# Release schedule

## KurrentDB versioning and release schedule

There are two categories of release for KurrentDB:
* Long term support (LTS) releases.
* Feature releases.

The version number reflects whether a release is an LTS or feature release. LTS releases have even major numbers, and feature releases have odd major numbers.

### Versioning scheme

The version scheme for KurrentDB is `Major.Minor.Patch` where:
* `Major`
    * Is odd for feature releases.
    * Is even for LTS releases.
* `Minor`
    * Increments with scope changes or new features.
    * Is typically `0` for LTS releases, but may be incremented in rare cases.
* `Patch` for bug/security fixes.

As an example, the future releases of KurrentDB may look like this:

| Version  | Type    | Description |
|----------|---------|-------------|
| `25.0.0` | Feature | The first feature release of KurrentDB. |
| `25.1.0` | Feature | A new feature added to KurrentDB. |
| `26.0.0` | LTS     | The first LTS release of KurrentDB. |
| `26.0.1` | LTS     | A patch to 26.0.0. |
| `27.0.0` | Feature | The next feature release, with new features. |

### Long term support releases

We aim to release one LTS version of KurrentDB per year.

These versions will be supported for a minimum of two years, with a two month grace period for organizing upgrades when the LTS goes out of support.

LTS versions of KurrentDB start with an even major number.

### Feature releases

Feature releases will be published when new features are added to KurrentDB.

These versions will be supported until the next major or minor version of KurrentDB.

Feature versions of KurrentDB start with an odd major number.

## Supported EventStoreDB versions

EventStoreDB had a different versioning scheme to KurrentDB, where the LTS release always had a minor version of `10`.

The LTS versions of EventStoreDB will still be supported for two years from their release date. This means the following versions of EventStoreDB are still within Kurrent's support window:

* [EventStoreDB 24.10](https://docs.kurrent.io/server/v24.10/quick-start/)
* [EventStoreDB 23.10](https://docs.kurrent.io/server/v23.10/quick-start/)
