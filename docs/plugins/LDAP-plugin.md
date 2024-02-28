# LDAP Authentication Plugin

The LDAP Authentication plugin enables EventStoreDB to use LDAP-based directory services for authentication. 

::: tip
The LDAP plugin is included in commercial versions of EventStoreDB.
:::

## Configuration Steps

To set up EventStoreDB with LDAP authentication, follow these steps on the [database node's configuration file](@server/configuration.md). Remember to stop the service before making changes, and then start it. 

1. Change the authentication type to `ldaps`.
2. Add a section named `LdapsAuth` for LDAP-specific settings. 

Example configuration file in YAML format:

```yaml
AuthenticationType: ldaps
LdapsAuth:
  Host: 13.88.9.49
  Port: 636 #to use plaintext protocol, set Port to 389 and UseSSL to false 
  UseSSL: true
  ValidateServerCertificate: false #set this to true to validate the certificate chain
  AnonymousBind: false
  BindUser: cn=binduser,dc=mycompany,dc=local
  BindPassword: p@ssw0rd!
  BaseDn: ou=Lab,dc=mycompany,dc=local
  ObjectClass: organizationalPerson
  Filter: sAMAccountName
  RequireGroupMembership: false #set this to true to allow authentication only if the user is a member of the group specified by RequiredGroupDn
  GroupMembershipAttribute: memberOf
  RequiredGroupDn: cn=ES-Users,dc=mycompany,dc=local
  PrincipalCacheDurationSec: 60
  LdapGroupRoles:
      'cn=ES-Accounting,ou=Staff,dc=mycompany,dc=local': accounting
      'cn=ES-Operations,ou=Staff,dc=mycompany,dc=local': it
      'cn=ES-Admins,ou=Staff,dc=mycompany,dc=local': '$admins'
```

Upon successful LDAP authentication, users are assigned roles based on their domain group memberships, as specified in the `LdapGroupRoles` section. EventStoreDB supports two built-in roles: `$admins` and `$ops`, which can be assigned to users. 

## Troubleshooting 

If you encounter issues, check the server's log. Common problems include: 

### Invalid bind credentials specified: 
- Confirm the `BindUser` and `BindPassword`.

### Exception during search - 'No such Object' or 'The object does not exist': 
- Verify the `BaseDn`.

### 'Server certificate error' or 'Connect Error - The authentication or decryption has failed': 
- Verify that the server certificate is valid. If it is a self-signed certificate, set `ValidateServerCertificate` to `false`.

### The LDAP server is unavailable:

-   Verify connectivity to the LDAP server from an EventStoreDB node (e.g. using `netcat` or `telnet`).
-   Verify the `Host` and `Port` parameters.
-   Verify that the server certificate is valid. If it is a self-signed certificate, set `ValidateServerCertificate` to `false`.

### Error authenticating with LDAP server. System.AggregateException: 

- Example error message: `One or more errors occurred. ---> System.NullReferenceException: Object reference not set to an instance of an object. at Novell.Directory.Ldap.Connection.connect(String host, Int32 port, Int32 semaphoreId)`
-   This packaging error may occur when setting `UseSSL: true` on Windows. Extract `Mono.Security.dll` to the _EventStore_ folder (where _EventStore.ClusterNode.exe_ is located) as a workaround. 

### No errors in server logs but cannot login:

-   Verify the `ObjectClass` and `Filter` parameters
-   If you have set `RequireGroupMembership` to `true`, verify that the user is part of the group specified by `RequiredGroupDn` and that the LDAP record has the `memberOf` attribute (specified by `GroupMembershipAttribute`).
