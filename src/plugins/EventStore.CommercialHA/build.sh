#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PRODUCTNAME="Event Store Commercial"
COMPANYNAME="Event Store Ltd"
COPYRIGHT="Copyright 2020 Event Store Ltd. All rights reserved."


# ------------ End of configuration -------------

CONFIGURATION="Release"
RUNTIME="linux-x64"

function usage() {
cat <<EOF
Usage:
  $0 [<version=0.0.0.0>] [<configuration=Debug|Release>] [<runtime=linux-x64>]

version: EventStore build version. Versions must be complete four part identifiers valid for use on a .NET assembly.

configuration: Build configuration. Valid configurations are: Debug, Release

runtime: Runtime Identifier (linux-x64, ubuntu.18.04-x64, etc)

EOF
    exit 1
}

function detectOS(){
    unameOut="$(uname -s)"
    case "${unameOut}" in
        Linux*)     os=Linux;;
        Darwin*)    os=MacOS;;
        *)          os="${unameOut}"
    esac

    if [[ "$os" != "Linux" && $os != "MacOS" ]] ; then
        echo "Unsupported operating system: $os. Only Linux and MacOS are supported."
        exit 1
    fi
    OS=$os
}

function checkParams() {
    if [[ "$1" == "-help" || "$1" == "--help" ]] ; then
        usage
    fi

    version=$1
    configuration=$2
    runtime=$3

    [[ $# -gt 3 ]] && usage

    if [[ "$version" == "" ]] ; then
        VERSIONSTRING="0.0.0.0"
        echo "Version defaulted to: 0.0.0.0"
    else
        VERSIONSTRING=$version
        echo "Version set to: $VERSIONSTRING"
    fi

    if [[ "$configuration" == "" ]]; then
        CONFIGURATION="Release"
        echo "Configuration defaulted to: $CONFIGURATION"
    else
        if [[ "$configuration" == "Release" || "$configuration" == "Debug" ]]; then
            CONFIGURATION=$configuration
            echo "Configuration set to: $CONFIGURATION"
        else
            echo "Invalid configuration: $configuration"
            usage
        fi
    fi

    if [[ "$runtime" == "" ]]; then
        RUNTIME="linux-x64"
        echo "Runtime defaulted to linux-64: $RUNTIME"
    else
        RUNTIME=$runtime
    fi
}

function buildCommercialPlugins {
    rm -rf bin/

    echo "Publishing EventStore.Auth.Ldaps to plugins directory"

	find . -type f \( -iname "*.csproj" ! -iname "*.Tests.csproj" \) -exec \
		dotnet publish '{}' -c $CONFIGURATION -r $RUNTIME /p:Platform=x64 /p:AssemblyVersion=$VERSIONSTRING \;
}

detectOS
checkParams "$1" "$2" "$3"

echo "Running from base directory: $BASE_DIR"
buildCommercialPlugins
