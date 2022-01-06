# Security options

On this page you find all the configuration options related to in-flight security of EventStoreDB.

The protocol security configuration depends a lot on the deployment topology and platform. We have created an interactive [configuration tool](../installation/README.md), which also has instructions on how to generate and install the certificates and configure EventStoreDB nodes to use them. 

Below you can find more details about each of the available security options.

## Running without security

Unlike previous versions, EventStoreDB v20+ is secure by default. It means that you have to supply valid certificates and configuration for the database node to work.

We realise that many users want to try out the latest version with their existing applications, and also run a previous version of EventStoreDB without any security in their internal networks.

For this to work, you can use the `Insecure` option:
 
| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--insecure` |
| YAML                 | `Insecure` |
| Environment variable | `EVENTSTORE_INSECURE` |

**Default**: `false`

::: warning
When running with protocol security disabled, everything is sent unencrypted over the wire. In the previous version it included the server credentials. Sending username and password over the wire without encryption is not secure by definition, but it might give a false sense of security. In order to make things explicit, EventStoreDB v20 **does not use any authentication and authorisation** (including ACLs) when running insecure.
:::

## Certificates configuration

In this section, you can find settings related to protocol security (HTTPS and TLS).

### Certificate common name

SSL certificates can be created with a common name (CN), which is an arbitrary string. Usually is contains the DNS name for which the certificate is issued. When cluster nodes connect to each other, they need to ensure that they indeed talk to another node and not something that pretends to be a node. Therefore, EventStoreDB expects the connecting party to have a certificate with a pre-defined CN `eventstoredb-node`. 

When using the Event Store [certificate generator](#certificate-generation-cli), the CN is properly set by default. However, you might want to change the CN and in this case, you'd also need to tell EventStoreDB what value it should expect instead of the default one, using the setting below:
 
| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-reserved-node-common-name` |
| YAML                 | `CertificateReservedNodeCommonName` |
| Environment variable | `EVENTSTORE_CERTIFICATE_RESERVED_NODE_COMMON_NAME` |

**Default**: `eventstoredb-node`


::: warning
Server certificates **must** have the internal and external IP addresses or DNS names as subject alternative names.
:::

### Trusted root certificates

When getting an incoming connection, the server needs to ensure if the certificate used for the connection can be trusted. For this to work, the server needs to know where trusted root certificates are located.

EventStoreDB will not use the default trusted root certificates store location of the platform. So, even if you use a certificate signed by a publicly trusted CA, you'd need to explicitly tell the node to use the OS default root certificate store. For certificates signed by a private CA, you just provide the path to the CA certificate file (but not the filename).

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--trusted-root-certificates-paths` |
| YAML                 | `TrustedRootCertificatesPath` |
| Environment variable | `EVENTSTORE_TRUSTED_ROOT_CERTIFICATES_PATH` |

**Default**: n/a

### Certificate file

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

If the certificate file doesn't contain the certificate private key, you need to tell the node where to find the key file using the `CertificatePrivateKeyFile` setting.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--certificate-private-key-file` |
| YAML                 | `CertificatePrivateKeyFile` |
| Environment variable | `EVENTSTORE_CERTIFICATE_PRIVATE_KEY_FILE` |

::: warning RSA private key
EventStoreDB expects the private key to be in RSA format. Check the first line of the key file and ensure that it looks like this:
```
-----BEGIN RSA PRIVATE KEY-----
```
If you have non-RSA private key, you can use `openssl` to convert it:
```bash
openssl rsa -in privkey.pem -out privkeyrsa.pem
```
:::

### Certificate store (Windows)

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



### Intermediate CA certificates

Intermediate certificates are supported by loading them from a [PEM](https://www.rfc-editor.org/rfc/rfc1422) or [PKCS #12](https://datatracker.ietf.org/doc/html/rfc7292) bundle. The node's certificate should be first in the bundle, followed by the intermediates (intermediates can be in any order but it would be good to keep it from leaf to root as per the usual convention). The certificate chain is validated on startup with the node's own certificate.

Additionally, in order to improve performance, the server will also try to bypass intermediate certificate downloads, when they are available on the system in the appropriate locations:

#### Steps on Linux:
The following script assumes EventStoreDB is running under the `eventstore` account.
```
sudo su eventstore --shell /bin/bash
dotnet tool install --global dotnet-certificate-tool
 ~/.dotnet/tools/certificate-tool add --file /path/to/intermediate.crt
```

#### Steps on Windows:
To import the intermediate certificate in the Certificate store, run the following PowerShell under the same account as EventStoreDB is running:
```PowerShell
Import-Certificate -FilePath .\path\to\intermediate.crt -CertStoreLocation Cert:\CurrentUser\CA
```

To import the intermediate certificate in the `Local Computer` store, run the following as `Administrator`:
```PowerShell
Import-Certificate -FilePath .\ca.crt -CertStoreLocation Cert:\LocalMachine\CA
```


## Certificate Generation CLI

Event Store provides the interactive Certificate Generation CLI, which creates certificates signed by a private, auto-generated CA for EventStoreDB. You can use the [configuration wizard](../installation/README.md), that will provide you exact CLI commands that you need to run to generates certificates matching your configuration. 

### Getting Started

CLI is available as Open Source project in the [Github Repository.](https://github.com/EventStore/es-gencert-cli) The latest release can be found under the [GitHub releases page.](https://github.com/EventStore/es-gencert-cli/releases)

We're releasing binaries for Windows, Linux and macOS. We also publish the tool as a Docker image.

Basic usage for Certificate Generation CLI:

```bash
./es-gencert-cli [options] <command> [args]
```

Getting help for a specific command:

```bash
./es-gencert-cli -help <command>
```

::: warning
If you are running EventStoreDB on Linux, remember that all certificate files should have restrictive rights, otherwise the OS won't allow using them.
Usually, you'd need to change rights for each certificate file to prevent the "permissions are too open" error.

You can do it by running the following command:
```bash
chmod 600 [file]
```
:::

#### Generating the CA certificate

As the first step CA certificate needs to be generated. It'll need to be trusted for each of the nodes and client environment. 

By default, the tool will create the `ca` directory in the `certs` directory you created. Two keys will be generated:
- `ca.crt` - public file that need to be used also for the nodes and client configuration,
- `ca.key` - private key file that should be used only in the nodes configuration (**Do not copy it to client environment**). 

CA certificate will be generated with pre-defined CN `eventstoredb-node`.

To generate CA certificate run:

```bash
./es-gencert-cli create-ca
```

You can customise generated cert by providing following params:

| Param | Description |
| :------------------- | :----- |
| `-days` | The validity period of the certificate in days (default: 5 years) |
| `-out` | The output directory (default: ./ca) |

Example:

```bash
./es-gencert-cli create-ca -out ./es-ca
```

#### Generating the Node certificate

You need to generate certificates signed by the CA for each node. They should be installed only on the specific node machine.

By default, the tool will create the `ca` directory in the `certs` directory you created. Two keys will be generated:
- `node.crt` - the public file that needs to be also used for the nodes and client configuration,
- `node.key` - the private key file that should be used only in the node's configuration (**Do not copy it to client environment**). 

To generate node certificate run command:

```bash
./es-gencert-cli -help create_node
```

You can customise generated cert by providing following params:

| Param | Description |
| :-- | :-- |
| `-ca-certificate` | The path to the CA certificate file (default: `./ca/ca.crt`) |
| `-ca-key` | The path to the CA key file (default: `./ca/ca.key`) |
| `-days` | The output directory (default: `./nodeX` where X is an auto-generated number) |
| `-out` | The output directory (default: `./ca`) |
| `-ip-addresses` | Comma-separated list of IP addresses of the node |
| `-dns-names` | Comma-separated list of DNS names of the node |

::: warning
While generating the certificate, you need to remember to pass internal end external:
- IP addresses to `-ip-addresses`: e.g. `127.0.0.1,172.20.240.1` and/or 
- DNS names to `-dns-names`: e.g. `localhost,node1.eventstore`
that will match the URLs that you will be accessing EventStoreDB nodes.
:::

Sample:

```
./es-gencert-cli-cli create-node -ca-certificate ./es-ca/ca.crt -ca-key ./es-ca/ca.key -out ./node1 -ip-addresses 127.0.0.1,172.20.240.1 -dns-names localhost,node1.eventstore
```

#### Running with Docker

You could also run the tool using Docker interactive container:

```bash
docker run --rm -i eventstore/es-gencert-cli <command> <options>
```

One useful scenario is to use the Docker Compose file tool to generate all the necessary certificates before starting cluster nodes.

Sample:

```yaml
version: "3.5"

services:
  setup:
    image: eventstore/es-gencert-cli:1.0.2
    entrypoint: bash
    user: "1000:1000"
    command: >
      -c "mkdir -p ./certs && cd /certs
      && es-gencert-cli create-ca
      && es-gencert-cli create-node -out ./node1 -ip-addresses 127.0.0.1,172.20.240.1 -dns-names localhost,node1.eventstore
      && es-gencert-cli create-node -out ./node1 -ip-addresses 127.0.0.1,172.20.240.2 -dns-names localhost,node2.eventstore
      && es-gencert-cli create-node -out ./node1 -ip-addresses 127.0.0.1,172.20.240.3 -dns-names localhost,node3.eventstore
      && find . -type f -print0 | xargs -0 chmod 666"
    container_name: setup
    volumes:
      - ./certs:/certs
```

See more in the [complete sample of docker-compose secured cluster configuration.](../installation/docker.md#use-docker-compose)

## Certificate installation on a client environment

To connect to EventStoreDB, you need to install the auto-generated CA certificate file on the client machine (e.g. machine where the client is hosted, or your dev environment).

#### Linux (Ubuntu, Debian)

1. Copy auto-generated CA file to dir `/usr/local/share/ca-certificates/`, e.g. using command: 
  ```bash
  sudo cp ca.crt /usr/local/share/ca-certificates/event_store_ca.crt
  ```
2. Update the CA store: 
  ```bash
  sudo update-ca-certificates
  ```
#### Windows

1. You can manually import it to the local CA cert store through `Certificates Local Machine Management Console`. To do that select **Run** from the **Start** menu, and then enter `certmgr.msc`. Then import certificate to `Trusted Root Certification`.
2. You can also run the PowerShell script instead:

```powershell
Import-Certificate -FilePath ".\certs\ca\ca.crt" -CertStoreLocation Cert:\CurrentUser\Root
```
#### MacOS

1. In the Keychain Access app on your Mac, select either the login or System keychain. Drag the certificate file onto the Keychain Access app. If you're asked to provide a name and password, type the name and password for an administrator user on this computer.
2. You can also run the bash script:

```bash
sudo security add-certificates -k /Library/Keychains/System.keychain ca.crt 
```

## TCP protocol security

Although TCP is disabled by default for external connections (clients), cluster nodes still use TCP for replication. If you aren't running EventStoreDB in insecure mode, all TCP communication will use TLS using the same certificates as SSL.

You can, however, disable TLS for both internal and external TCP.

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-internal-tcp-tls` |
| YAML                 | `DisableInternalTcpTls` |
| Environment variable | `EVENTSTORE_DISABLE_INTERNAL_TCP_TLS` |

**Default**: `false`

| Format               | Syntax |
| :------------------- | :----- |
| Command line         | `--disable-external-tcp-tls` |
| YAML                 | `DisableExternalTcpTls` |
| Environment variable | `EVENTSTORE_DISABLE_EXTERNAL_TCP_TLS` |

**Default**: `false`

