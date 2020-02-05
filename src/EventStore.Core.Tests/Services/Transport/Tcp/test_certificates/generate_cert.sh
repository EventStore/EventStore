#!/bin/bash

if [ "$#" -ne 2 ]; then
    echo "generate_cert.sh <certificate_name> <common_name>"
    exit
fi

certificate_name=$1
common_name=$2

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
pushd DIR &>/dev/null

echo "Creating certificate directory: $certificate_name"
mkdir "$certificate_name"
pushd "$certificate_name" &>/dev/null

echo "Generating key: $certificate_name.key"
openssl genrsa -out "$certificate_name".key 2048 &>/dev/null

echo "Generating CSR: $certificate_name.csr"
openssl req -new -sha256 -key "$certificate_name".key -subj "/C=MU/ST=PW/O=EventStore, Inc./CN=$common_name" -out "$certificate_name".csr  &>/dev/null

echo "Generating Certificate: $certificate_name.crt"
openssl x509 -req -in "$certificate_name".csr -CA ../ca/ca.crt -CAkey ../ca/ca.key  -passin pass:password -CAcreateserial -out "$certificate_name".crt -days 10000 -sha256   &>/dev/null

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