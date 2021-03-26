# Authentication

EventStoreDB supports authentication based on usernames and passwords out of the box. The Enterprise version also supports LDAP as the authentication source.

Authentication is applied to all HTTP endpoints, except `/info`, `/ping`, `/stats`, `/elections` (only `GET`), `/gossip` (only `GET`) and static web content.

## Default users

EventStoreDB provides two default users, `$ops` and `$admin`.

`$admin` has full access to everything in EventStoreDB. It can read and write to protected streams, which is any stream that starts with \$, such as `$projections-master`. Protected streams are usually system streams, for example, `$projections-master` manages some of the projections' states. The `$admin` user can also run operational commands, such as scavenges and shutdowns on EventStoreDB.

An `$ops` user can do everything that an `$admin` can do except manage users and read from system streams (except for `$scavenges` and `$scavenges-streams`).

## New users

New users created in EventStoreDB are standard non-authenticated users. Non-authenticated users are allowed `GET` access to the `/info`, `/ping`, `/stats`, `/elections`, and `/gossip` system streams.

`POST` access to the `/elections` and `/gossip` system streams is only allowed on the internal HTTP service.

By default, any user can read any non-protected stream unless there is an ACL preventing that.

## Externalised authentication

You can also use the trusted intermediary header for externalized authentication that allows you to integrate almost any authentication system with EventStoreDB. Read more about [the trusted intermediary header](trusted-intermediary.md).

## Disable HTTP authentication

It is possible to disable authentication on all protected HTTP endpoints by setting the `DisableFirstLevelHttpAuthorization` setting to `true`. The setting is set to `false` by default. When enabled, the setting will force EventStoreDB to use the supplied credentials only to check the stream access using [ACLs](acl.md). 
