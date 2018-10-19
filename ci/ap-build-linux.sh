#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

dotnet build -c $EventStoreBuildConfig src/EventStore.sln