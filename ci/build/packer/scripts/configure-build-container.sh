#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

function configure_mono_repo {
	rpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"

	curl "https://download.mono-project.com/repo/centos7-stable.repo" | \
		tee "/etc/yum.repos.d/mono-centos7-stable.repo"
}

function install_mono {
	yum update -y
	yum install -y mono-devel mkbundle msbuild git
}

configure_mono_repo
install_mono
