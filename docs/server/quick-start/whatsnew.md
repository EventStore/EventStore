---
order: 2
---

# What's New

## New features

* Archiving
* KurrentDB rebranding

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
