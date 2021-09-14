# Setting up SSL for Docker

This example shows how to build own Docker image based on the original EventStoreDB image with a built-in self-signed certificate. The example should be modified before going to production by using real certificate.

Create file `Dockerfile` with following content:

```dockerfile
FROM eventstore/eventstore:release-5.0.8
RUN apt-get update -y \
  && apt-get install -y openssl \
  && openssl req -x509 -sha256 -nodes -days 3650 -subj "/CN=eventstore.org" -newkey rsa:2048 -keyout eventstore.pem -out eventstore.csr \
  && openssl pkcs12 -export -inkey eventstore.pem -in eventstore.csr -out eventstore.p12 -passout pass: \
  && openssl pkcs12 -export -inkey eventstore.pem -in eventstore.csr -out eventstore.pfx -passout pass: \
  && mkdir -p /usr/local/share/ca-certificates \
  && cp eventstore.csr /usr/local/share/ca-certificates/eventstore.crt \
  && update-ca-certificates \
  && apt-get autoremove \
  && apt-get clean \
  && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
```

Build the image:

```bash
docker build -t eventstore/eventstore:with-cert-local --no-cache .
```

Run the container:

```bash
docker run --name eventstore-node -it -p 1113:1113 -p 1115:1115 -p 2113:2113 -e EVENTSTORE_CERTIFICATE_FILE=eventstore.p12 -e EVENTSTORE_EXT_SECURE_TCP_PORT=1115 eventstore/eventstore:with-cert-local
```

Notice we use port `1115` instead of default `1113`.

Use the following connection string for the TCP client:

```
"ConnectTo=tcp://localhost:1115;DefaultUserCredentials=admin:changeit;UseSslConnection=true;TargetHost=eventstore.org;ValidateServer=false",
```

Notice `ValidateServer` is set to `false` because we use a self-signed certificate. For production always use `ValidateServer=true`.

