#!/usr/bin/env bash

set -e
set -x

source_directory=$1
output_directory=$2

tests=$(find "$source_directory" -maxdepth 1 -type d -name "*.Tests")

# Publish tests
for test in $tests; do
    if [ "$test" = "/build/src/EventStore.Auth.Ldaps.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Auth.OAuth.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Auth.StreamPolicyPlugin.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Auth.UserCertificates.Tests" ] || 
       [ "$test" = "/build/src/EventStore.AutoScavenge.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Diagnostics.LogsEndpointPlugin.Tests" ] || 
       [ "$test" = "/build/src/EventStore.Security.EncryptionAtRest.Tests" ] || 
       [ "$test" = "/build/src/EventStore.TcpPlugin.Tests" ]; then
      echo Skipping tests for $test
        continue
    fi

    echo Publishing tests for $test

    dotnet publish \
      --runtime="$RUNTIME" \
      --no-self-contained \
      --configuration Release \
      --output "$output_directory/$(basename "$test")" \
      "$test"
done

cp "$source_directory"/*.sln "$output_directory"
