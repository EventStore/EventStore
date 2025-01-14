#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

dotnet --version

# Ensure the "first run experience" isn't co-mingled with build output but does get
# run. It appears that if it is _not_ run, builds fail.
cd "${TMPDIR}" && dotnet new

#Install aws cli
sudo python -m pip install --upgrade pip setuptools wheel
sudo pip install awscli