#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store LLP"
COPYRIGHT="Copyright 2012 Event Store LLP. All rights reserved."


# ------------ End of configuration -------------

function usage() {
    echo <<EOF 
Usage:
  $0 [<version=0.0.0.0>] [<configuration=release>] [<distro-platform-override>]

Versions must be complete four part idenfitiers valid for use on a .NET assembly.

Valid configurations are:
  debug
  release

The OS distribution and version will be detected automatically unless it is
overriden as the last argument. This script expects to find libjs1.[so|dylib]
in the src/libs/x64/distroname-distroversion/ directory, built using the scripts
in the scripts/build-js1/ directory. Note that overriding this may result in
crashes using Event Store.

*The only supported Linux for production use at the moment is Ubuntu 14.04 LTS.*
However, since several people have asked for builds compatible with Amazon Linux
in particular, we have included a pre-built version of libjs1.so which will
link to the correct version of libc on Amazon Linux 2015.03.

Currently the supported versions without needing to build libjs1 from source are:
  ubuntu-14.04              (Ubuntu Trusty)
  amazon-2015.03            (Amazon Linux 2015.03)

EOF
    exit 1
}

CONFIGURATION="Release"
DEFINES="USE_UNIX_IO MONO"

function checkParams() {
    version=$1
    configuration=$2
    platform_override=$3

    [[ $# -gt 3 ]] && usage

    if [[ "$configuration" == "" ]]; then
        CONFIGURATION="release"
        echo "Configuration defaulted to: $CONFIGURATION"
    else
        if [[ "$configuration" == "release" || "$configuration" == "debug" ]]; then
            CONFIGURATION=$configuration
            echo "Configuration set to: $CONFIGURATION"
        else
            echo "Invalid configuration: $configuration"
            usage
        fi
    fi

    if [[ "$version" == "" ]] ; then
        VERSIONSTRING="0.0.0.0"
        echo "Version defaulted to: 0.0.0.0"
    else
        VERSIONSTRING=$version
        echo "Version set to: $VERSIONSTRING"
    fi

    if [[ "$platform_override" == "" ]] ; then
        # shellcheck source=../detect-system/detect-system.sh disable=SC1091
        source "$BASE_DIR/scripts/detect-system/detect-system.sh"
        getSystemInformation
        CURRENT_DISTRO="$ES_DISTRO-$ES_DISTRO_VERSION"
    else
        CURRENT_DISTRO=$platform_override
    fi
    LIBJS1EXT="so"
    if [ "$ES_DISTRO" == "osx" ]; then
        LIBJS1EXT="dylib"
    fi
    LIBJS1PATH="$BASE_DIR/src/libs/x64/$CURRENT_DISTRO/libjs1.$LIBJS1EXT"
    if [ ! -f "$LIBJS1PATH" ]; then
        echo "$LIBJS1PATH does not exist. Did you build libjs1 for this distribution/version?"
        exit 1
    fi
}

function revertVersionFiles() {
    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        git checkout "$file"
        echo "Reverted $file"
    done
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
    revertVersionFiles
    revertVersionInfo
    echo "FAILED. See earlier messages"
    exit 1
}

function patchVersionFiles {
    branchName=$(git rev-parse --abbrev-ref HEAD)
    commitHashAndTime=$(git log -n1 --pretty=format:"%H@%aD" HEAD)

    newAssemblyVersion="[assembly: AssemblyVersion(\"$VERSIONSTRING\")]"
    newAssemblyFileVersion="[assembly: AssemblyFileVersion(\"$VERSIONSTRING\")]"
    newAssemblyVersionInformational="[assembly: AssemblyInformationalVersion(\"$VERSIONSTRING.$branchName@$commitHashAndTime\")]"
    newAssemblyProductName="[assembly: AssemblyProduct(\"$PRODUCTNAME\")]"
    newAssemblyCopyright="[assembly: AssemblyCopyright(\"$COPYRIGHT\")]"
    newAssemblyCompany="[assembly: AssemblyCompany(\"$COMPANYNAME\")]"

    assemblyVersionPattern='.*AssemblyVersion(.*'
    assemblyFileVersionPattern='.*AssemblyFileVersion(.*'
    assemblyVersionInformationalPattern='.*AssemblyInformationalVersion(.*'
    assemblyProductNamePattern='.*AssemblyProduct(.*'
    assemblyCopyrightPattern='.*AssemblyCopyright(.*'
    assemblyCompanyPattern='.*AssemblyCompany(.*'

    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        tempfile="$file.tmp"
        sed -e "s/$assemblyVersionPattern/$newAssemblyVersion/g" \
            -e "s/$assemblyFileVersionPattern/$newAssemblyFileVersion/g" \
            -e "s/$assemblyVersionInformationalPattern/$newAssemblyVersionInformational/g" \
            -e "s/$assemblyProductNamePattern/$newAssemblyProductName/g" \
            -e "s/$assemblyCopyrightPattern/$newAssemblyCopyright/g" \
            -e "s/$assemblyCompanyPattern/$newAssemblyCompany/g" \
            "$file" > "$tempfile" || err

        mv "$tempfile" "$file"

        if grep "AssemblyInformationalVersion" "$file" > /dev/null ; then
            echo "Patched $file with version information"
        else
            echo " " >> "$file"
            echo "$newAssemblyVersionInformational" >> "$file"
            echo "Patched $file with version information"
        fi
    done
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

function linkCurrentJS1 {
    mkdir -p "$BASE_DIR/src/libs/x64/current"
    for f in $BASE_DIR/src/libs/x64/$CURRENT_DISTRO/*; do
        ln -s "$f" "$BASE_DIR/src/libs/x64/current/$(basename "$f")"
    done
}

function buildEventStore {
    patchVersionFiles
    patchVersionInfo
    rm -rf bin/
    xbuild src/EventStore.sln /p:Platform="Any CPU" /p:DefineConstants="$DEFINES" /p:Configuration="$CONFIGURATION" || err
    revertVersionFiles
    revertVersionInfo
}

function exitWithError {
    echo "$1"
    exit 1
}

checkParams "$1" "$2" "$3"

echo "Running from base directory: $BASE_DIR"
echo "Running on distribution: $CURRENT_DISTRO"
linkCurrentJS1
buildEventStore
rm -rf "$BASE_NAME/src/libs/x64/$CURRENT_DISTRO"
