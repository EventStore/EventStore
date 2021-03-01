# Compatibility notes

Depending on how your EventStoreDB instance is configured, some of the features might not work. On this page, you can find some notes about features being not available because of some options are set or not set accordingly.

| Feature | Options impact |
| :------ | :------------- |
| Connection for TCP clients | External TCP is disabled by default. You need to enable it explicitly by using the `EnableExternalTcp` option. |
| Connection without SSL or TLS | EventStoreDB 20.6+ is secure by default. Your clients need to establish a secure connection, unless you use the `Insecure` option. |
| Authentication and ACLs | When using the `Insecure` option for the server, all the security gets disabled. You also get the `Users` menu item disabled in the Admin UI. |
| Projections | Running projections is disabled by default and the `Projections` menu item is disabled in the Admin UI. You need to enable projections explicitly by using the `RunProjections` option.
| AtomPub protocol | In 20.6+, the AtomPub protocol is disabled by default. If you use this protocol, you have to explicitly enable it by using the `EnableAtomPubOverHttp` option. |
| Stream browser | The stream browser feature in Admin UI depends on the AtomPub protocol and also gets greyed out by default. You need to enable AtomPub (previous line) to make the stream browser work. |

