#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

# Install Mono from Homebrew, and force link it if there is an existing installation
if ! brew install mono ; then
	brew link --overwrite mono
fi

# Print the versions of mono and dotnet which are on PATH
mono --version
dotnet --version

# Ensure the "first run experience" isn't co-mingled with build output but does get
# run. It appears that if it is _not_ run, builds fail.
cd "${TMPDIR}" && dotnet new