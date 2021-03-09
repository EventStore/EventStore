# macOS

## Install with Homebrew

EventStoreDB has a macOS package [you can download](https://eventstore.com/downloads/) and install. We also maintain a Homebrew Cask formula you can install:

```bash
brew cask install eventstore
```

In each case you can run EventStoreDB with the `eventstore` command, and stop it with `Ctrl+C`. To use the default database location you need to use `sudo`, which we **strongly** recommend you don't do, or you can change the location with the `--db` parameter to a location your user account has access to.

To build EventStoreDB from source, refer to the EventStoreDB [GitHub repository](https://github.com/EventStore/EventStore#mac-os-x).

## Uninstall

If you installed EventStoreDB using Homebrew, you can remove it with one of the following:

```shell
brew cask uninstall eventstore
```

If you built EventStoreDB from source, remove it by deleting the directory containing the source and build, and manually removing any environment variables.

