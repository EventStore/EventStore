---
order: 2
---

# What's New

## New features in 25.0

These are the new features in KurrentDB 25.0:

* [Archiving](#archiving)
* [KurrentDB rebranding](#kurrentdb-rebranding)
* [New versioning scheme and release schedule](#new-versioning-scheme-and-release-schedule)

### Archiving

<Badge type="info" vertical="middle" text="License Required"/>

KurrentDB 25.0 introduces the initial release of Archiving: a new major feature to reduce costs and increase scalability of a KurrentDB cluster.

With the new Archiving feature, data is uploaded to cheaper storage such as Amazon S3 and then can be removed from the volumes attached to the cluster nodes. The volumes can be correspondingly smaller and cheaper. The nodes are all able to read the archive, and when a read request from a client requires data that is stored in the archive, the node retrieves that data from the archive transparently to the client.

Refer to [the documentation](../features/archiving.md) for more information about archiving and instructions on how to set it up.

### KurrentDB rebranding

Event Store – the company and the product – are rebranding as Kurrent.

As part of this rebrand, EventStoreDB has been renamed to KurrentDB, with the first release of KurrentDB being version 25.0.

Read more about the rebrand in the [rebrand FAQ](https://www.kurrent.io/blog/kurrent-re-brand-faq).

The KurrentDB packages are still hosted on [Cloudsmith](https://cloudsmith.io/~eventstore/repos/kurrent/packages/). Refer to [the upgrade guide](./upgrade-guide.md) to see what's changed between EventStoreDB and KurrentDB, or [the installation guide](./installation.md) for updated installation instructions.

### New versioning scheme and release schedule

We are changing the version scheme with the first official release of KurrentDB.

As before, there will be two categories of release:
* Long term support (LTS) releases which are supported for a minimum of two years, with a 2 month grace period.
* Feature releases which are supported until the next major or minor release.

The version number will now reflect whether a release is an LTS or feature release, rather than being based on the year. LTS releases will have even major numbers, and feature releases will have odd major numbers.

#### Versioning scheme

The new scheme is `Major.Minor.Patch` where:
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
| `27.0.0` | Feature | The next feature release, with new features |

#### New release schedule

The release schedule will be changing with the versioning scheme, given that the version numbers are no longer tied to the year and month:

* LTS: At least 1 LTS release per year.
* Feature: Published as necessary when new features are ready.
* Patch (LTS and feature): Published as necessary with bugfixes and/or security patches.
