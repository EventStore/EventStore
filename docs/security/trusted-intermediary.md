# Trusted Intermediary

The trusted intermediary header helps EventStoreDB to support a common security architecture. There are thousands of possible methods for handling authentication and it is impossible for us to support them all. The header allows you to configure a trusted intermediary to handle the authentication instead of EventStoreDB. 

A sample configuration is to enable OAuth2 with the following steps:

- Configure EventStoreDB to run on the local loopback.
- Configure nginx to handle OAuth2 authentication.
- After authenticating the user, nginx rewrites the request and forwards it to the loopback to EventStoreDB that serves the request.

The header has the form of `{user}; group, group1` and the EventStoreDB ACLs use the information to handle security.

```http
ES-TrustedAuth: "root; admin, other"
```

Use the following option to enable this feature:

| -EnableTrustedAuth<br/>--enable-trusted-auth=VALUE<br/> | ENABLE_TRUSTED_AUTH 

