---
title: User authorization
order: 3
---

# Authorization

Authorization governs what an authenticated user can access and what actions they can perform, based on that user's roles.

## Roles

Users are granted roles by the [authentication mechanism](./user-authentication.md). These roles are then used to determine what operations, endpoints, and streams a user has access to.

EventStoreDB provides the `$admins` and `$ops` roles by default, granted respectively to the `admin` and `ops` default users. Custom roles can also be created.

### Admins role

The `$admins` role is granted to the `admin` user by default, and authorizes the user to do the following:

- Read all streams, including `$all` and system streams.
- Manage users and reset their passwords.
- Perform all administrative operations on a node or cluster, such as scavenges, restarts, and triggering leader elections.
- Manage projections and persistent subscriptions.
- View statistics for the node, subscriptions, or projections

You can create additional admin users by assigning the `$admins` group to a user.

::: note
The `$admins` role bypasses all stream access checks such as [ACLs](#access-control-lists) or [stream policies](#stream-policy-authorization).
:::

### Ops role

The `$ops` role is granted to the `ops` user by default. This role grants similar permissions to the `$admins` role, but without the user management and stream access.

The `$ops` role authorizes the user to do the following:

- Perform maintenance operations on a node or cluster, such as scavenges, restarts, and triggering leader elections.
- Manage projections and persistent subscriptions.
- View statistics for the node, subscriptions, or projections
- Stream access is determined by the [authorization mechanism used](#stream-access)

You can create additional ops users by assigning the `$ops` group to a user.

### Custom roles

Custom roles are any role assigned to an authenticated user, outside of the `$admins` and `$ops` roles.
These can be groups assigned to the user, their username, or a mapping from the authentication mechanism used. These roles can be anything, but typically describe the group or tenant that a user is part of. Example of custom roles are `accounting`, `sales`, or `tenant-123`.

Users with custom (or no) roles are authorized to do the following:

- View statistics for the node, subscriptions, or projections
- Stream access is determined by the [authorization mechanism used](#stream-access)

::: tip
Users managed in EventStoreDB are always granted a role that matches their username.
:::

## Stream access

Stream access covers what actions a user can perform on a stream within EventStoreDB. These actions are:

| Name              | Key   | Description |
|-------------------|-------|-------------|
| Read              | `$r`  | The permission to read from a stream. |
| Write             | `$w`  | The permission to write to a stream. |
| Delete            | `$d`  | The permission to delete a stream. |
| Metadata read     | `$mr` | The permission to read the metadata associated with a stream. |
| Metadata write    | `$mw` | The permission to write the metadata associated with a stream. |

EventStoreDB supports these methods of authorizing user access to streams:
- [Access control lists](#access-control-lists) to restrict user access to streams based on metadata in each stream.
- [Stream policies](#stream-policy-authorization) to restrict user acces to streams based on configured policies.

### Access control lists

By default, authenticated users have access to all non-system streams in the EventStoreDB database. You can use Access Control Lists (ACLs) to set up more granular access control. In fact, the default access
level is also controlled by a special ACL, which is called the [default ACL](#default-acl).

#### Stream ACL

EventStoreDB keeps the ACL of a stream in the stream metadata as JSON with the below definition:

```json
{
  "$acl": {
    "$w": "$admins",
    "$r": "$all",
    "$d": "$admins",
    "$mw": "$admins",
    "$mr": "$admins"
  }
}
```

These fields represent the following:

- `$w` The permission to write to this stream.
- `$r` The permission to read from this stream.
- `$d` The permission to delete this stream.
- `$mw` The permission to write the metadata associated with this stream.
- `$mr` The permission to read the metadata associated with this stream.

You can update these fields with either a single string or an array of strings representing users or
groups (`$admins`, `$all`, or custom groups). It's possible to put an empty array into one of these fields,
and this has the effect of removing all users from that permission.

::: tip 
We recommend you don't give people access to `$mw` as then they can then change the ACL.
:::

#### Default ACL

The `$settings` stream has a special ACL used as the default ACL. This stream controls the default ACL for
streams without an ACL and also controls who can create streams in the system, the default state of these is
shown below:

```json
{
  "$userStreamAcl": {
    "$r": "$all",
    "$w": "$all",
    "$d": "$all",
    "$mr": "$all",
    "$mw": "$all"
  },
  "$systemStreamAcl": {
    "$r": "$admins",
    "$w": "$admins",
    "$d": "$admins",
    "$mr": "$admins",
    "$mw": "$admins"
  }
}
```

You can rewrite these to the `$settings` stream with the following request:

@[code{curl}](@samples/default-settings.sh)

The `$userStreamAcl` controls the default ACL for user streams, while all system streams use
the `$systemStreamAcl` as the default.

::: tip 
Members of `$admins` always have access to everything, you cannot remove this permission.
:::

When you set a permission on a stream, it overrides the default values. However, it's not necessary to specify
all permissions on a stream. It's only necessary to specify those which differ from the default.

Here is an example of the default ACL that has been changed:

```json
{
  "$userStreamAcl": {
    "$r": "$all",
    "$w": "ouro",
    "$d": "ouro",
    "$mr": "ouro",
    "$mw": "ouro"
  },
  "$systemStreamAcl": {
    "$r": "$admins",
    "$w": "$admins",
    "$d": "$admins",
    "$mr": "$admins",
    "$mw": "$admins"
  }
}
```

This default ACL gives `ouro` and `$admins` create and write permissions on all streams, while everyone else
can read from them. Be careful allowing default access to system streams to non-admins as they would also have
access to `$settings` unless you specifically override it.

Refer to the documentation of the HTTP API or SDK of your choice for more information about changing ACLs
programmatically.

### Stream Policy Authorization

<Badge type="info" vertical="middle" text="License Required"/>

This allows administrators to define stream access policies for EventStoreDB based on stream prefix.

#### Enabling

You require a [license key](../quick-start/installation.md#license-keys) to use this feature.

The stream access policy is configured by the last event in the `$authorization-policy-settings` stream.

Enable stream policies by appending an event of type `$authorization-policy-changed` to the `$authorization-policy-settings` stream:

::: tabs
@tab HTTP
```http
POST https://localhost:2113/streams/$authorization-policy-settings
Authorization: Basic admin changeit
Content-Type: application/json
ES-EventId: 11887e82-9fb4-4112-b937-aea895b32a4a
ES-EventType: $authorization-policy-changed

{
    "streamAccessPolicyType": "streampolicy"
}
```

@tab Powershell
```powershell
$JSON = @"
{
    "streamAccessPolicyType": "streampolicy"
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -H "ES-EventId: 11887e82-9fb4-4112-b937-aea895b32a4b" `
  -H "ES-EventType: `$authorization-policy-changed" `
  -u "admin:changeit" `
  -d $JSON `
  https://localhost:2113/streams/%24authorization-policy-settings
```

@tab Bash
```bash
JSON='{
    "streamAccessPolicyType": "streampolicy"
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -H "ES-EventId: 814f6d67-515c-4bd6-a6d6-8a0235ec719a" \
  -H "ES-EventType: \$authorization-policy-changed" \
  -u "admin:changeit" \
  -d "$JSON" \
  https://localhost:2113/streams/%24authorization-policy-settings
```
:::

And disable stream policies by setting the stream access policy type back to `acl`:

::: tabs
@tab HTTP

```http
### Configure cluster to use acls
POST https://localhost:2113/streams/$authorization-policy-settings
Authorization: Basic admin changeit
Content-Type: application/json
ES-EventId: d07a6637-d9d0-43b7-8f48-6a9a8800af12
ES-EventType: $authorization-policy-changed

{
    "streamAccessPolicyType": "acl"
}
```

@tab Powershell
```Powershell
$JSON = @"
{
    "streamAccessPolicyType": "acl"
}
"@ `

curl.exe -X POST `
  -H "Content-Type: application/json" `
  -H "ES-EventId: 3762c89e-d12e-4a01-83bb-781459c93ebc" `
  -H "ES-EventType: `$authorization-policy-changed" `
  -u "admin:changeit" `
  -d $JSON `
  https://localhost:2113/streams/%24authorization-policy-settings
```
@tab Bash
```bash
JSON='{
    "streamAccessPolicyType": "acl"
}'

curl -X POST \
  -H "Content-Type: application/json" \
  -H "ES-EventId: d6b3a316-2493-4bcd-9019-e02b5ae4eec3" \
  -H "ES-EventType: \$authorization-policy-changed" \
  -u "admin:changeit" \
  -d "$JSON" \
  https://localhost:2113/streams/%24authorization-policy-settings
```

:::

When stream policies are enabled, you should see the following logs:

You can check that the feature is enabled by searching for the following log at startup:

```
[22860,28,17:30:42.247,INF] StreamBasedAuthorizationPolicyRegistry New Authorization Policy Settings event received
[22860,28,17:30:42.249,INF] StreamBasedAuthorizationPolicyRegistry Starting factory streampolicy
...
[22860,28,17:30:42.264,INF] StreamBasedPolicySelector      Existing policy found in $policies stream (0)
[22860,28,17:30:42.269,INF] StreamBasedPolicySelector      Successfully applied policy
[22860,28,17:30:42.271,INF] StreamBasedPolicySelector      Subscribing to $policies at 0
```

If you do not have a valid license, stream policies will log the following error and will not start. EventStoreDB will continue to run with the current policy type:

```
[22928, 1,17:31:56.879,INF] StreamPolicySelector           Stream Policies plugin is not licensed, stream policy authorization may not be enabled.
...
[22928,28,17:31:57.030,ERR] StreamPolicySelector           Stream Policies plugin is not licensed, cannot enable Stream policies
[22928,28,17:31:57.030,ERR] StreamBasedAuthorizationPolicyRegistry Failed to enable policy selector plugin streampolicy. Authorization settings will not be applied
```

::: note
If you enable the Stream Policy feature, EventStoreDB will not enforce [stream ACLs](#access-control-lists).
:::

#### Overriding the default

If the `$authorization-policy-settings` stream is empty and there is no configuration, EventStoreDB will default to using ACLs.

If you would rather default to stream policies, you can do this by setting the `Authorization:DefaultPolicyType` option to `streampolicy`.

::: note
Any events in the `$authorization-policy-settings` stream take precedence over the `Authorization:DefaultPolicyType` setting.
:::

You can do this by adding the following to the server configuration file:

```yaml
Authorization:
  DefaultPolicyType: streampolicy
```

Refer to the [configuration guide](../configuration/README.md) for configuration mechanisms other than YAML.

::: warning
If the policy type configured in `Authorization:DefaultPolicyType` is not present at startup, EventStoreDB will not start.
:::

#### Fallback stream access policy

If none of the events in the `$authorization-policy-settings` stream are valid, or if the stream policy plugin could not be started (for example, due to it being unlicensed), EventStoreDB will fall back to restricted access.

This locks down all stream access to admins only to prevent EventStoreDB from falling back to a more permissive policy such as ACLs.

To recover from this, either fix the issue preventing the plugin from loading, or set the stream access policy type to a different policy such as ACL:

```json
{
    "streamAccessPolicyType": "acl"
}
```

#### Stream Policies and Rules

Stream policies and Stream Rules are configured by events written to the `$policies` stream.

##### Default Stream Policy

When the Stream Policy feature is run for the first time, it will create a default policy in the `$policies` stream.
The default policy does the following
- Grants public access to user streams (this excludes users in the `$ops` group).
- Restricts system streams to the `$admins` group.
- Grants public read and metadata read access to the default projection streams (`$ce`, `$et`, `$bc`, `$category`, `$streams`).

EventStoreDB will log when the default policy is created:

```
[31124,16,11:18:15.099,DBG] StreamBasedPolicySelector Successfully wrote default policy to stream $policies.
```

::: details Click here to see the default policy

```
{
  "streamPolicies": {
    "publicDefault": {
      "$r": ["$all"],
      "$w": ["$all"],
      "$d": ["$all"],
      "$mr": ["$all"],
      "$mw": ["$all"]
    },
    "adminsDefault": {
      "$r": ["$admins"],
      "$w": ["$admins"],
      "$d": ["$admins"],
      "$mr": ["$admins"],
      "$mw": ["$admins"]
    },
    "projectionsDefault": {
      "$r": ["$all"],
      "$w": ["$admins"],
      "$d": ["$admins"],
      "$mr": ["$all"],
      "$mw": ["$admins"]
    }
  },
  "streamRules": [
    {
      "startsWith": "$et-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$ce-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$bc-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$category-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$streams",
      "policy": "projectionsDefault"
    }
  ],
  "defaultStreamRules": {
    "userStreams": "publicDefault",
    "systemStreams": "adminsDefault"
  }
}
```

:::

::: note
Operations users in the `$ops` group are excluded from the `$all` group and do not have access to user streams by default.
:::

##### Custom Stream Policies

You can create a custom stream policy by writing an event with event type `$policy-updated` to the `$policies` stream.

Define your custom policy in the `streamPolicies`, e.g:

```
"streamPolicies": {
    "customPolicy": {
        "$r": ["ouro", "readers"],
        "$w": ["ouro"],
        "$d": ["ouro"],
        "$mr": ["ouro"],
        "$mw": ["ouro"]
    },
    // Default policies truncated (full version defined below)
}
```

And then add an entry to `streamRules` to specify the stream prefixes which should use the custom policy. You can apply the same policy to multiple streams by defining multiple stream rules. e.g:

```
"streamRules": [{
    "startsWith": "account",
    "policy": "customPolicy"
}, {
    "startsWith": "customer",
    "policy": "customPolicy"
}]
```

You still need to specify default stream rules when you update the `$policies` stream.

::: details Click here to see the above example in full

```
{
  "streamPolicies": {
    "customPolicy": {
        "$r": ["ouro", "readers"],
        "$w": ["ouro"],
        "$d": ["ouro"],
        "$mr": ["ouro"],
        "$mw": ["ouro"]
    },
    "publicDefault": {
      "$r": ["$all"],
      "$w": ["$all"],
      "$d": ["$all"],
      "$mr": ["$all"],
      "$mw": ["$all"]
    },
    "adminsDefault": {
      "$r": ["$admins"],
      "$w": ["$admins"],
      "$d": ["$admins"],
      "$mr": ["$admins"],
      "$mw": ["$admins"]
    },
    "projectionsDefault": {
      "$r": ["$all"],
      "$w": ["$admins"],
      "$d": ["$admins"],
      "$mr": ["$all"],
      "$mw": ["$admins"]
    }
  },
  "streamRules": [
    {
      "startsWith": "account",
      "policy": "customPolicy"
    },
    {
      "startsWith": "customer",
      "policy": "customPolicy"
    },
    {
      "startsWith": "$et-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$ce-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$bc-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$category-",
      "policy": "projectionsDefault"
    },
    {
      "startsWith": "$streams",
      "policy": "projectionsDefault"
    }
  ],
  "defaultStreamRules": {
    "userStreams": "publicDefault",
    "systemStreams": "adminsDefault"
  }
}
```

:::

::: note
If a policy update is invalid, it will not be applied and an error will be logged. EventStoreDB will continue running with the previous valid policy in place.
:::

##### Stream Policy Schema

###### Policy

| Key                   | Type                                  | Description | Required |
|-----------------------|---------------------------------------|-------------|----------|
| `streamPolicies`      | `Dictionary<string, AccessPolicy>`    | The named policies which can be applied to streams. | Yes |
| `streamRules`         | `StreamRule[]`                        | An array of rules to apply for stream access.  The ordering is important, and the first match wins. | Yes |
| `defaultStreamRules`  | `DefaultStreamRules`                  | The default policies to apply if no other policies are defined for a stream | Yes |

###### AccessPolicy

| Key   | Type	    | Description	| Required |
|-------|-----------|---------------|----------|
| `$r`  | string[]  | User and group names that are granted stream read access. | Yes |
| `$w`  | string[]  | User and group names that are granted stream write access. | Yes |
| `$d`  | string[]  | User and group names that are granted stream delete access. | Yes |
| `$mr` | string[]  | User and group names that are granted metadata stream read access. | Yes |
| `$mw` | string[]  | User and group names that are granted metadata stream write access. | Yes |

::: note
Having metadata read or metadata write access to a stream does not grant read or write access to that stream.
:::

###### DefaultStreamRules

|Key                | Type           | Description   | Required |
|-------------------|----------------|---------------|----------|
| `userStreams`     | `AccessPolicy` | The default policy to apply for non-system streams. | Yes |
| `systemStreams`   | `AccessPolicy` | The default policy to apply for system streams. | Yes |

###### StreamRule

| Key           | Type     | Description   | Required |
|---------------|----------|---------------|----------|
| `startsWith`  | `string` | The stream prefix to apply the rule to. | Yes |
| `policy`      | `string` | The name of the policy to enforce for streams that match this prefix. | Yes |
