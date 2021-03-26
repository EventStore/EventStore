# Default directories

The default directories used by EventStoreDB vary by platform to fit with the common practices each platform.

## Linux

When you install EventStoreDB from PackageCloud on Linux or using Homebrew on macOS, the following locations apply:

-   **Application:** `/usr/bin`
-   **Content:** `/usr/share/eventstore`
-   **Configuration:** `/etc/eventstore/`
-   **Data:** `/var/lib/eventstore`
-   **Server logs:** `/var/log/eventstore`
-   **Test client logs:** `./testclientlog`
-   **Web content:** `./clusternode-web` then `{Content}/clusternode-web`
-   **Projections:** `./projections` then `{Content}/projections`
-   **Prelude:** `./Prelude` then `{Content}/Prelude`

## Windows

-   **Configuration:** `./`
-   **Data:** `./data`
-   **Server logs:** `./logs`
-   **Test client log:** `./testclientlogs`
-   **Web content:** `./clusternode-web`
-   **Projections:** `./projections`
-   **Prelude:** `./Prelude`

## macOS

When you install EventStoreDB using Homebrew on macOS, the cask files are installed to `/usr/local/Caskroom/eventstore/5.0.8/EventStore-OSS-MacOS-macOS-v5.0.8`.

Homebrew will create symlinks to the server and test client executables:

- **Server:** `/usr/local/bin/eventstore`
- **Test client:** `/usr/local/bin/eventstore-testclient`

When started, the server will look for the `eventstore.conf` and `log.config` configuration files at its location.

- **Data:** `/var/lib/eventstore`
- **Server logs:** `/var/log/eventstore`
- **Test client logs:** `./testclientlog`
- **Web content:** `./clusternode-web`
- **Projections:** `./projections`
- **Prelude:** `./Prelude`

::: tip macOS note
On macOS you will get permissions error if you run `eventstore` without `sudo`. We advise changing the configuration file and change the `Db` [option](../server/database.md#database-location) to a place where you have access as the normal user.
:::

## Local binaries

When running EventStoreDB using local binaries, either downloaded or built from source, the server will use its location and place the necessary files inside it:

-   **Configuration:** `./`
-   **Data:** `./data`
-   **Server logs:** `./logs`
-   **Test client log:** `./testclientlogs`
-   **Web content:** `./clusternode-web`
-   **Projections:** `./projections`
-   **Prelude:** `./Prelude`
