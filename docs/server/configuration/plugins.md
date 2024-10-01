---
title: Plugins
order: 7
---

# Plugins configuration

The commercial edition of EventStoreDB ships with several plugins that augment the behavior of the open source server. Each plugin is documented in relevant sections. For example, under Security, you will find the User Certificates plugin documentation.

The plugins (apart from the `ldap` plugin) are configured separately to the main server configuration and can be configured via `json` files and `environment variables`.

Environment variables take precedence.

## JSON files

Each configurable plugin comes with a sample JSON file in the `<installation-directory>/plugins/<plugin-name>` directory.
Here is an example file called `user-certificates-plugin-example.json` to enable the user certificates plugin.

```json
{
  "EventStore": {
    "Plugins": {
      "UserCertificates": {
        "Enabled": true
      }
    }
  }
}
```

The system looks for JSON configuration files in a `<installation-directory>/config/` directory, and on Linux and OS X, the server additionally looks in `/etc/eventstore/config/`. To take effect, JSON configuration must be saved to one of these locations.

The JSON configuration may be split across multiple files or combined into a single file. Apart from the `json` extension, the names of the files is not important.

## Environment variables

Any configuration can alternatively be provided via environment variables.

Use `__` as the level delimiter.

For example, the key configured above in json can also be set with the following environment variable:

```
EventStore__Plugins__UserCertificates__Enabled
```
