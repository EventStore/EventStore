# Linux

## Install from PackageCloud

EventStoreDB has pre-built [packages available for Debian-based distributions](https://packagecloud.io/EventStore/EventStore-OSS), [manual instructions for distributions that use RPM](https://packagecloud.io/EventStore/EventStore-OSS/install#bash-rpm), or you can [build from source](https://github.com/EventStore/EventStore#linux). The final package name to install is `eventstore-oss`.

If you installed from a pre-built package, the server gets registered as a service. Therefore, you can start EventStoreDB with:

```bash
sudo systemctl start eventstore
```

When you install the EventStoreDB package, the service doesn't start by default. It's done to allow you to change the configuration, located at _/etc/eventstore/eventstore.conf_ and to prevent creating database and index files in the default location.

::: warning
We recommend that when using Linux you set the 'open file limit' to a high number. The precise value depends on your use case, but at least between `30,000` and `60,000`.
:::

## Building from source

You can also build EventStoreDB on Linux from source. Before doing that, you need to install Mono. We recommend [Mono 5.16.0](https://www.mono-project.com/download/stable/), but other versions may also work. EventStoreDB packages have Mono embedded, so you don't need to install anything except the EventStoreDB package.

## Uninstall

If you installed one of the [pre-built packages for Debian based systems](https://packagecloud.io/EventStore/EventStore-OSS), you can remove it with:

```bash
sudo apt-get purge eventstore-oss
```

This removes EventStoreDB completely, including any user settings.

If you built EventStoreDB from source, remove it by deleting the directory containing the source and build, and manually removing any environment variables.
