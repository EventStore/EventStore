# Access Control Lists

By default, authenticated users have access to the whole EventStoreDB database. In addition to that, it allows you to use Access Control Lists (ACLs) to set up more granular access control. In fact, the default access level is also controlled by a special ACL, which is called the [default ACL](#default-acl).

## Stream ACL

EventStoreDB keeps the ACL of a stream in the streams metadata as JSON with the below definition:

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

You can update these fields with either a single string or an array of strings representing users or groups (`$admins`, `$all`, or custom groups). It's possible to put an empty array into one of these fields, and this has the effect of removing all users from that permission.

::: tip
We recommend you don't give people access to `$mw` as then they can then change the ACL.
:::

## Default ACL

The `$settings` stream has a special ACL used as the default ACL. This stream controls the default ACL for streams without an ACL and also controls who can create streams in the system, the default state of these is shown below:

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

<<< @/samples/default-settings.sh#curl

The `$userStreamAcl` controls the default ACL for user streams, while all system streams use the `$systemStreamAcl` as the default.

::: tip
The `$w` in `$userStreamAcl` also applies to the ability to create a stream. Members of `$admins` always have access to everything, you cannot remove this permission.
:::

When you set a permission on a stream, it overrides the default values. However, it's not necessary to specify all permissions on a stream. It's only necessary to specify those which differ from the default.

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

This default ACL gives `ouro` and `$admins` create and write permissions on all streams, while everyone else can read from them. Be careful allowing default access to system streams to non-admins as they would also have access to `$settings` unless you specifically override it.

Refer to the documentation of the HTTP API or SDK of your choice for more information about changing ACLs programmatically.

