#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

# Install Mono
curl https://download.mono-project.com/archive/5.16.0/macos-10-universal/MonoFramework-MDK-5.16.0.220.macos10.xamarin.universal.pkg -o mono.pkg
sudo installer -store -pkg mono.pkg -target /

# Print the versions of mono and dotnet which are on PATH
mono --version
dotnet --version

# Ensure the "first run experience" isn't co-mingled with build output but does get
# run. It appears that if it is _not_ run, builds fail.
cd "${TMPDIR}" && dotnet new