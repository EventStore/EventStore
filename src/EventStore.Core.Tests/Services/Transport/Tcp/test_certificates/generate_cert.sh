#!/bin/bash

if [ "$#" -ne 1 ]; then
    echo "generate_cert.sh <certificate_name>"
    echo "generate_cert.sh node1"
    exit
fi

certificate_name=$1

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
pushd DIR &>/dev/null

echo "Creating certificate directory: $certificate_name"
mkdir "$certificate_name"
pushd "$certificate_name" &>/dev/null

echo "Generating key: $certificate_name.key"
openssl genrsa -out "$certificate_name".key 2048 &>/dev/null

echo "Generating CSR: $certificate_name.csr"
openssl req -new -sha256 -key "$certificate_name".key -subj "/CN=eventstoredb-node" -out "$certificate_name".csr  &>/dev/null

echo "Generating Certificate: $certificate_name.crt"
openssl x509 \
    -req \
    -sha256 \
    -days 10000 \
    -in "$certificate_name".csr \
    -CA ../ca/ca.pem -CAkey ../ca/ca.key \
    -passin pass:password \
    -CAcreateserial \
    -extfile <(\
    printf "
        authorityKeyIdentifier=keyid,issuer
        basicConstraints=CA:FALSE
        keyUsage = digitalSignature, nonRepudiation, dataEncipherment
        subjectAltName = IP:127.0.0.1,DNS:localhost"
    ) \
    -out "$certificate_name".crt &>/dev/null

echo "Generating PKCS#12 certificate: $certificate_name.p12"
openssl pkcs12 -export -inkey "$certificate_name".key -in "$certificate_name".crt -out "$certificate_name".p12 -password pass:password  &>/dev/null

echo "Deleting CSR, certificate and key file"
rm "$certificate_name".csr &>/dev/null
rm "$certificate_name".crt &>/dev/null
rm "$certificate_name".key &>/dev/null
rm .srl &>/dev/null

popd &>/dev/null

popd &>/dev/null

echo "Done!"
