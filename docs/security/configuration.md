# Security options

On this page you find all the configuration options related to in-flight security of EventStoreDB.

## Certificates configuration

SSL certificates can be created with a common name that includes the machine DNS name or IP address. You can instruct EventStoreDB to check the incoming connection certificate to have such a common name, so the node can be sure that it's another cluster node, which is trying to connect.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ssl-target-host` |
| YAML                 | `SslTargetHost` |
| Environment variable | `EVENTSTORE_SSL_TARGET_HOST` |

**Default**: n/a

The certificate validation setting tells EventStoreDB to ensure that the incoming connection uses the trusted certificate. This setting should normally be set to `true` for production environments. It will only work if the cluster nodes use certificated signed by the trusted public CA. In case of self-signed certificates, the CA certificate needs to be added to the trusted certificate store of the machine.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ssl-validate-server` |
| YAML                 | `SslValidateServer` |
| Environment variable | `EVENTSTORE_SSL_VALIDATE_SERVER` |

**Default**: `true`, so the server will only accept connection that use trusted certificates.

It is possible to configure cluster nodes to check if the connecting party is another node by setting the expected common name of the certificate. When the EventStoreDB server receives the incoming connection with a matching CN, it will assume it is another cluster node. Omitting this setting increases the risk of malicious actors connecting to the cluster and pretending to be cluster nodes.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-subject-name` |
| YAML                 | `CertificateSubjectName` |
| Environment variable | `EVENTSTORE_CERTIFICATE_SUBJECT_NAME` |

Other certificate settings determine the location of the certificate to be used by the cluster node. Configuration for the certificate location is platform-specific.

### Windows

Check the [SSL on Windows](ssl-windows.md) guidelines for detailed instructions.

The certificate store location is the location of the Windows certificate store, for example `CurrentUser`.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-store-location` |
| YAML                 | `CertificateStoreLocation` |
| Environment variable | `EVENTSTORE_CERTIFICATE_STORE_LOCATION` |

The certificate store name is the name of the Windows certificate store, for example `My`.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-store-name` |
| YAML                 | `CertificateStoreName` |
| Environment variable | `EVENTSTORE_CERTIFICATE_STORE_NAME` |

You need to add the certificate thumbprint setting on Windows so the server can ensure that it's using the correct certificate found in the certificates store.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-thumbprint` |
| YAML                 | `CertificateThumbprint` |
| Environment variable | `EVENTSTORE_CERTIFICATE_THUMBPRINT` |

# Linux

Check the [SSL on Linux](ssl-linux.md) guidelines for detailed instructions.

The `CertificateFile` setting needs to point to the certificate file, which will be used by the cluster node.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-file` |
| YAML                 | `CertificateFile` |
| Environment variable | `EVENTSTORE_CERTIFICATE_FILE` |

If the certificate file is protected by password, you'd need to set the `CertificatePassword` value accordingly, so the server can load the certificate.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-password` |
| YAML                 | `CertificatePassword` |
| Environment variable | `EVENTSTORE_CERTIFICATE_PASSWORD` |

## Protocol configuration

EventStoreDB supports encryption for both TCP and HTTP. Each protocol can be made secure on its own.

Unlike HTTP, which will use the internal HTTP port setting for SSL connections, secure TCP port needs to be specified explicitly.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-secure-tcp-port` |
| YAML                 | `IntSecureTcpPort` |
| Environment variable | `EVENTSTORE_INT_SECURE_TCP_PORT` |

**Default**: `0`, you need to specify the port for secure TCP to work.

If you use some kind of port mapping between the actual internal TCP port and the port, which is exposed to other machines in the subnet, you can use the `IntSecureTcpPortAdvertiseAs` setting to instruct EventStoreDB cluster node to use a different port value for the internal gossip.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--int-secure-tcp-port-advertise-as` |
| YAML                 | `IntSecureTcpPortAdvertiseAs` |
| Environment variable | `EVENTSTORE_INT_SECURE_TCP_PORT_ADVERTISE_AS` |

**Default**: `0`, the internal secure TCP port will be advertised using the value of the `IntSecureTcpPort` setting.

For external clients to connect over the secure TLS channel, you'd need to set the `ExtSecureTcpPort` setting. Use the port value in the connection settings of your applications.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--ext-secure-tcp-port` |
| YAML                 | `ExtSecureTcpPort` |
| Environment variable | `EVENTSTORE_EXT_SECURE_TCP_PORT` |

**Default**: `0`, the setting must be set when enabling TLS.

As for the `InternalTcpPortAdvertiseAs`, the same can be done for the external TCP port if the exposed port on the machine doesn't match the port exposed to the outside world. The setting value will be used for the gossip endpoint response for connecting clients.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--` |
| YAML                 | `` |
| Environment variable | `EVENTSTORE_` |

**Default**: `0`, so the external gossip endpoint will advertise the value specified in the `ExtSecureTcpPort`

You can fully disable insecure TCP connection by setting `DisableInsecureTCP` to `false`. By default, even if TLS is enabled, it would be still possible to connect via TCP without using TLS, unless this setting is changed.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-insecure-tcp=VALUE` |
| YAML                 | `DisableInsecureTCP` |
| Environment variable | `EVENTSTORE_DISABLE_INSECURE_TCP` |

**Default**: `false`, so clients can connect via insecure TCP.

## Internal secure communication

By default, cluster nodes use internal HTTP and TCP ports and communicate without applying any transport security. This can be changed by setting the `UseInternalSsl` setting to `true`.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--use-internal-ssl` |
| YAML                 | `UseInternalSsl` |
| Environment variable | `EVENTSTORE_USE_INTERNAL_SSL` |

**Default**: `false`, nodes within the cluster communicate over plain HTTP and TCP
