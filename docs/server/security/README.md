# Security

For production use it is important to configure EventStoreDB security features to prevent unauthorised access to your data.

Security features of EventStoreDB include:
 
- [User management](authentication.md) for allowing users with different roles to access the database
- [Access Control Lists](acl.md) to restrict access to specific event streams
- Encryption in-flight using HTTPS and TLS

## Protocol security

EventStoreDB supports the proprietary TCP protocol for high-throughput real-time communication, and the more traditional HTTP REST API for read and append operations, as well as for the management operations like scavenging, creating projections and so on. EventStoreDB also uses HTTP for the gossip seed endpoint, both internally for the cluster gossip, and internally for clients that connect to the cluster using discovery mode.

Both those protocols support encryption with TLS and SSL. Each protocol has its own security configuration, but you can only use one set of certificates for both TLS and HTTPS.

The process of creating certificates and instructing EventStoreDB to use them is different per platform. Please follow the platform-specific guidelines for setting up SSL and TLS:

- [Setting up SSL on Linux](ssl-linux.md)
- [Setting up SSL on Windows](ssl-windows.md)
- [Setting up SSL for Docker](ssl-docker.md)

These guidelines also include some platform-specific configuration settings, so you should be able to run a secure cluster after completing the steps described there.




