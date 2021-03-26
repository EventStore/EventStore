# Security

For production use it is important to configure EventStoreDB security features to prevent unauthorised access to your data.

Security features of EventStoreDB include:
 
- [User management](authentication.md) for allowing users with different roles to access the database
- [Access Control Lists](acl.md) to restrict access to specific event streams
- Encryption in-flight using HTTPS and TLS

## Protocol security

EventStoreDB supports gRPC and the proprietary TCP protocol for high-throughput real-time communication. It also has some HTTP endpoints for the management operations like scavenging, creating projections and so on. EventStoreDB also uses HTTP for the gossip seed endpoint, both internally for the cluster gossip, and internally for clients that connect to the cluster using discovery mode.

All those protocols support encryption with TLS and SSL. Each protocol has its own security configuration, but you can only use one set of certificates for both TLS and HTTPS.

The protocol security configuration depends a lot on the deployment topology and platform. We have created an interactive [configuration tool](../installation/README.md), which also has instructions on how to generate and install the certificates and configure EventStoreDB nodes to use them. 


