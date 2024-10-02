---
order: 4
---

# Default directories

The default directories used by EventStoreDB vary by platform to fit with the common practices each platform.

## Linux

When you install EventStoreDB on Linux, the following locations apply:

- **Application:** `/usr/bin`
- **Content:** `/usr/share/eventstore`
- **Configuration:** `/etc/eventstore/`
- **Data:** `/var/lib/eventstore`
- **Server logs:** `/var/log/eventstore`
- **Test client logs:** `./testclientlog`
- **Web content:** `./clusternode-web` then `{Content}/clusternode-web`
- **Projections:** `./projections` then `{Content}/projections`
- **Prelude:** `./Prelude` then `{Content}/Prelude`

## Windows

- **Configuration:** `./`
- **Data:** `./data`
- **Server logs:** `./logs`
- **Test client log:** `./testclientlogs`
- **Web content:** `./clusternode-web`
- **Projections:** `./projections`
- **Prelude:** `./Prelude`

## Local binaries

When running EventStoreDB using local binaries, either downloaded or built from source, the server will use its location and place the necessary files inside it:

- **Configuration:** `./`
- **Data:** `./data`
- **Server logs:** `./logs`
- **Test client log:** `./testclientlogs`
- **Web content:** `./clusternode-web`
- **Projections:** `./projections`
- **Prelude:** `./Prelude`

Depending on the platform and installation type, the location of EventStoreDB executables, configuration and other necessary files vary.
