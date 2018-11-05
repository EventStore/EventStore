#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

find ./src -maxdepth 1 -type d -name '*.Tests' -print0| xargs -0 -n1 dotnet test -v normal -c $EventStoreBuildConfig --logger trx