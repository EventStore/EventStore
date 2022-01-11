# Intermediate CA certificates

Intermediate CA certificates are supported by loading them from a [PEM](https://datatracker.ietf.org/doc/html/rfc1422) or [PKCS #12](https://datatracker.ietf.org/doc/html/rfc7292) bundle specified by the [`CertificateFile` configuration parameter](./configuration.md#certificate-file). To make sure that the configuration is correct, the certificate chain is validated on startup with the node's own certificate.

## Am I using intermediate CA certificates?
If you've used the [certificate generation tool](./configuration.md#certificate-generation-cli) with the default settings to generate your CA and node certificates, then you're not using intermediate CA certificates.  
​<br/>
However, if you're using a public certificate authority (e.g [Let's Encrypt](https://letsencrypt.org/)) to generate your node certificates there is a chance that you're using intermediate CA certificates without knowing. This is due to the [Authority Information Access (AIA)](https://datatracker.ietf.org/doc/html/rfc4325#section-2) extension which allows intermediate certificates to be fetched from a remote server.  
​<br/>
To verify if your certificate is using the AIA extension, you need to verify if there is a section named: `Authority Information Access` in the certificate.

### Steps on Linux:
```
openssl x509 -in /path/to/node.crt -text | grep 'Authority Information Access' -A 1
```

### Steps on Windows:
Open the certificate, go to the `Details` tab and look for the `Authority Information Access` field.

If the extension is present, you can manually download the intermediate certificate from the URL present under the `CA Issuers` entry.
Note that you will usually need to convert the downloaded certificate from the `DER` to the `PEM` format.  
​<br/>
This can be done with the following openssl command:
```
openssl x509 -inform der -in /path/to/cert.der > /path/to/cert.pem
```

or with [an online service](https://www.sslshopper.com/ssl-converter.html) if you don't have openssl installed.

It's possible that there are more than one intermediate CA certificates in the chain - so you need to verify if the certificate you've just downloaded also uses the AIA extension. If yes, you need to download the next intermediate CA certificate in the chain by repeating the same process above until you eventually reach a publicly trusted root certificate (i.e. the `Subject` and `Issuer` fields will match). In practice, there'll usually be at most two intermediate certificates in the chain.

## Bundling the intermediate certificates
The node's certificate should be first in the bundle, followed by the intermediates. Intermediates can be in any order but it would be good to keep it from leaf to root, as per the usual convention. The root certificate should not be bundled.

In the examples below, intermediate certificates are numbered from 1 to N starting from the leaf and going up.

### PEM format
​
If your node's certificate and the intermediate CA certificates are both PEM formatted, that is they begin with `-----BEGIN CERTIFICATE-----` and end with `-----END CERTIFICATE-----` then you can simply append the contents of the intermediate certificate files to the end of the node's certificate file to create the bundle.
​
### Steps on Linux:
```
cat /path/to/intermediate1.crt >> /path/to/node.crt
...
cat /path/to/intermediateN.crt >> /path/to/node.crt
```

### Steps on Windows:
```
type C:\path\to\intermediate1.crt >> C:\path\to\node.crt
...
type C:\path\to\intermediateN.crt >> C:\path\to\node.crt
```

### PKCS #12 format
If you want to generate a PKCS #12 bundle from PEM formatted certificate files, please follow the steps below.
### Steps on Linux:
```
cat /path/to/intermediate1.crt >> ./ca_bundle.crt
...
cat /path/to/intermediateN.crt >> ./ca_bundle.crt


openssl pkcs12 -export -in /path/to/node.crt -inkey /path/to/node.key -certfile ./ca_bundle.crt -out /path/to/node.p12 -passout pass:<password>
```

## Preventing intermediate certificate downloads
If your certificates use the AIA extension as described in [this section](#am-i-using-intermediate-ca-certificates), to improve performance you can additionally follow these steps to prevent the EventStoreDB server from downloading intermediate certificates by putting them on the system in the appropriate locations:

### Steps on Linux:
The following script assumes EventStoreDB is running under the `eventstore` account.
```
sudo su eventstore --shell /bin/bash
dotnet tool install --global dotnet-certificate-tool
 ~/.dotnet/tools/certificate-tool add --file /path/to/intermediate.crt
```

### Steps on Windows:
To import the intermediate certificate in the Certificate store, run the following PowerShell command under the same account as EventStoreDB is running:
```PowerShell
Import-Certificate -FilePath .\path\to\intermediate.crt -CertStoreLocation Cert:\CurrentUser\CA
```

To import the intermediate certificate in the `Local Computer` store, run the following as `Administrator`:
```PowerShell
Import-Certificate -FilePath .\ca.crt -CertStoreLocation Cert:\LocalMachine\CA
```