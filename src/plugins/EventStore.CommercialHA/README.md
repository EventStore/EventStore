## Releasing the Plugins

You can trigger a release of all of the plugins by pushing a commercial version tag to the repo in the form of `commercial-v*`. e.g. `commercial-v23.6.0`.

You can also manually trigger a release of just one plugin using the `Release Dispatch` workflow and providing the following inputs:

| Name | Description | Example |
|---|---|---|
| Version | The version without prefixes | 23.6.0 |
| Project | The plugin project without the EventStore prefix | Auth.Ldaps |

### Branches & Tags
* Latest verions are tagged from main.
* Tags for patch releases are generally based on the previous version/tag, i.e.: `git tag commercial-v22.10.3 commercial-v22.10.2`.
* Create a release branch incase updates have to be applied, i.e.: `release/commercial-v21.10`.

## Running the LDAPS Plugin

The ldaps plugin can be tested using a docker container provided [here](https://github.com/rroemhild/docker-test-openldap).

1. Pull and run the docker container:

```
docker pull rroemhild/test-openldap
docker run --rm -p 10389:10389 -p 10636:10636 rroemhild/test-openldap
```

2. Ensure the ldaps plugin dlls are in a folder called `ldaps` in the Event Store plugins location.

3. Add the following ldaps config to your Event Store configuration:

```
---
AuthenticationType: ldaps
LdapsAuth:
  Host: 127.0.0.1
  Port: 10636
  ValidateServerCertificate: false
  UseSSL: true
  AnonymousBind: false
  BindUser: cn=admin,dc=planetexpress,dc=com
  BindPassword: GoodNewsEveryone
  BaseDn: dc=planetexpress,dc=com
  ObjectClass: organizationalPerson
  Filter: uid
  GroupMembershipAttribute: memberOf
  RequireGroupMembership: false
  RequiredGroupDn: 'cn=admin_staff,ou=people,dc=planetexpress,dc=com'
  PrincipalCacheDurationSec: 60
  LdapGroupRoles:
    'cn=admin_staff,ou=people,dc=planetexpress,dc=com': '$admins'
    'cn=ship_crew,ou=people,dc=planetexpress,dc=com': 'crew'
```

4. Log in with the admin user (more users can be found on the ldaps docker container page):

```
Username: professor
Password: professor
```
