#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store LLP"
COPYRIGHT="Copyright 2018 Event Store LLP. All rights reserved."


# ------------ End of configuration -------------

CONFIGURATION="Release"
BUILD_UI="no"
NET_FRAMEWORK_API="4.7.1-api"
MONO_LIB_DIR_LINUX="/usr/lib/mono"
MONO_LIB_DIR_MAC="/Library/Frameworks/Mono.framework/Versions/5.16.0/lib/mono"

function usage() {    
cat <<EOF
Usage:
  $0 [<version=0.0.0.0>] [<configuration=Debug|Release>] [<build_ui=yes|no>] [<mono_lib_dir_override>]

version: EventStore build version. Versions must be complete four part identifiers valid for use on a .NET assembly.

configuration: Build configuration. Valid configurations are: Debug, Release

build_ui: Whether or not to build the EventStore UI. Building the UI requires an installation of Node.js (v8.11.4+)

mono_lib_dir_override: Overrides the default mono lib directory. The default directories are as follows:
  Linux: $MONO_LIB_DIR_LINUX
  MacOS: $MONO_LIB_DIR_MAC
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
    mono_lib_dir_override=$4

    [[ $# -gt 4 ]] && usage

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

    mono_lib_dir=""
    if [ "$OS" == "Linux" ]; then
        mono_lib_dir="$MONO_LIB_DIR_LINUX"
    elif [ "$OS" == "MacOS" ]; then
        mono_lib_dir="$MONO_LIB_DIR_MAC"
    fi

    if [[ "$mono_lib_dir_override" != "" ]] ; then
        mono_lib_dir=$mono_lib_dir_override
    fi

    if [ ! -d $mono_lib_dir ]; then
        echo "Error: Mono library directory does not exist: $mono_lib_dir"
        exit 1
    fi

    export FrameworkPathOverride=$mono_lib_dir/$NET_FRAMEWORK_API

    echo "Mono library directory set to: $mono_lib_dir"
    echo "FrameworkPathOverride set to: $FrameworkPathOverride"
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

    npm install bower@~1.8.4 -g
    bower install --allow-root
    npm install gulp@~3.8.8 -g
    npm install
    gulp dist
    mv es-dist ../EventStore.ClusterNode.Web/clusternode-web/
    popd
}

function buildEventStore {
    patchVersionFiles
    patchVersionInfo
    rm -rf bin/
    dotnet build -c $CONFIGURATION src/EventStore.sln || err
    revertVersionFiles
    revertVersionInfo
}

function exitWithError {
    echo "$1"
    exit 1
}

detectOS
checkParams "$1" "$2" "$3" "$4"

echo "Running from base directory: $BASE_DIR"
buildUI
buildEventStore
