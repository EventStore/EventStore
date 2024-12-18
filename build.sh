#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store Ltd"
COPYRIGHT="Copyright 2021 Event Store Ltd. All rights reserved."


# ------------ End of configuration -------------

CONFIGURATION="Release"
BUILD_UI="no"
NET_FRAMEWORK="net9.0"

function usage() {    
cat <<EOF
Usage:
  $0 [<version=0.0.0.0>] [<configuration=Debug|Release>] [<build_ui=yes|no>]

version: EventStore build version. Versions must be complete four part identifiers valid for use on a .NET assembly.

configuration: Build configuration. Valid configurations are: Debug, Release

build_ui: Whether or not to build the EventStore UI. Building the UI requires an installation of Node.js (v8.11.4+)

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
    build_ui=$3

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

    if [[ "$build_ui" == "" ]]; then
        BUILD_UI="no"
        echo "Build UI defaulted to: $BUILD_UI"
    else
        if [[ "$build_ui" == "yes" || "$build_ui" == "no" ]]; then
            BUILD_UI=$build_ui
            echo "Build UI set to: $BUILD_UI"
        else
            echo "Invalid Build UI value: $build_ui"
            usage
        fi
    fi
}

function revertVersionInfo() {
    files=$( find . -name "VersionInfo.cs" )

    for file in $files
    do
        git checkout "$file"
        echo "Reverted $file"
    done
}

function err() {
    revertVersionInfo
    echo "FAILED. See earlier messages"
    exit 1
}

function patchVersionInfo {
    branchName=$(git rev-parse --abbrev-ref HEAD)
    commitHash=$(git log -n1 --pretty=format:"%H" HEAD)
    commitTimestamp=$(git log -n1 --pretty=format:"%aD" HEAD)

    newVersion="public static readonly string Version = \"$VERSIONSTRING\";"
    newBranch="public static readonly string Branch = \"$branchName\";"
    newCommitHash="public static readonly string Hashtag = \"$commitHash\";"
    newTimestamp="public static readonly string Timestamp = \"$commitTimestamp\";"

    versionPattern="public static readonly string Version .*$"
    branchPattern="public static readonly string Branch .*$"
    commitHashPattern="public static readonly string Hashtag .*$"
    timestampPattern="public static readonly string Timestamp .*$"

    files=$( find . -name "VersionInfo.cs" )

    for file in $files
    do
        tempfile="$file.tmp"
        sed -e "s/$versionPattern/$newVersion/" \
            -e "s/$branchPattern/$newBranch/" \
            -e "s/$commitHashPattern/$newCommitHash/" \
            -e "s/$timestampPattern/$newTimestamp/" \
            "$file" > "$tempfile"

        mv "$tempfile" "$file"
        echo "Patched $file with version information"
    done
}

function buildUI {
    if [[ "$BUILD_UI" != "yes" ]] ; then
        echo "Skipping UI Build"
        return
    fi

    rm -rf src/EventStore.ClusterNode.Web/clusternode-web/
    pushd src/EventStore.UI

    if [ ! -f ./package.json ]; then
        git submodule update --init ./
    fi

    npm install bower@~1.8.14 -g
    bower install --allow-root
    npm install gulp-cli -g
    npm install
    gulp dist
    mv es-dist ../EventStore.ClusterNode.Web/clusternode-web/
    popd
}

function buildEventStore {
    patchVersionInfo
    rm -rf bin/
    dotnet build -c $CONFIGURATION /p:Platform=x64 /p:Version=$VERSIONSTRING --framework=$NET_FRAMEWORK src/EventStore.sln || err
    revertVersionInfo
}

function exitWithError {
    echo "$1"
    exit 1
}

detectOS
checkParams "$1" "$2" "$3"

echo "Running from base directory: $BASE_DIR"
buildUI
buildEventStore
