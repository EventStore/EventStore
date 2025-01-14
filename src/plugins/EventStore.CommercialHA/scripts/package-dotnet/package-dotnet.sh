#!/usr/bin/env bash
#Tarball packaging script for Linux & OS X with dotnet included

set -o errexit
set -o pipefail

function usage {
    echo "Usage:"
    echo "  $0 [<version=0.0.0.0>] [<build=oss|commercial>] [<targetRunTime=linux-x64>] [<distro=Ubuntu-18.04>] [<distro_shortname=ubuntu>]"
}

if [[ "$1" == "-help" || "$1" == "--help" ]] ; then
    usage
    exit
fi

baseDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
scriptName=$(basename ${BASH_SOURCE[0]})

version=$1
build=$2
targetRunTime=$3
distro=$4
distro_shortname=$5

Configuration="Release"
OS=""

function writeLog {
    message=$1
    echo "[$scriptName] - $message"
}

function detectOS(){
    unameOut="$(uname -s)"
    case "${unameOut}" in
        Linux*)     os=Linux;;
        Darwin*)    os=MacOS;;
        *)          os="${unameOut}"
    esac

    if [[ "$os" != "Linux" && $os != "MacOS" ]] ; then
        writeLog "Unsupported operating system: $os. Only Linux and MacOS are supported."
        exit 1
    fi
    OS=$os
}

function publishProject {
    projectDirectory=$1
    writeLog "Publishing $projectDirectory for $targetRunTime to $PACKAGEDIRECTORY"

    dotnet publish $projectDirectory -c $Configuration -r $targetRunTime -o $PACKAGEDIRECTORY --no-build /p:Version=$version /p:Platform=x64
}

function preCopy {
    if [[ "$version" == "" ]] ; then
        VERSIONSTRING="0.0.0.0"
        writeLog "Version defaulted to: 0.0.0.0"
    else
        VERSIONSTRING=$version
        writeLog "Version set to: $VERSIONSTRING"
    fi

    if [[ "$build" == "" ]] ; then
        BUILD="oss"
        writeLog "Build defaulted to: oss"
    elif [[ "$build" != "oss" && "$build" != "commercial" ]] ; then
        BUILD="oss"
        writeLog "Invalid build specified: $build. Build set to: oss"
    else
        BUILD=$build
        writeLog "Build set to: $build"
    fi

    OUTPUTDIR="$baseDir/../../bin/packaged"
    [[ -d $OUTPUTDIR ]] || mkdir -p "$OUTPUTDIR"

    detectOS

    soext=""
    if [ "$OS" == "Linux" ]; then
        soext="so"
    elif [ "$OS" == "MacOS" ]; then
        soext="dylib"
    fi

    PACKAGENAME="EventStore"
    if [[ "$BUILD" == "oss" ]]; then
        PACKAGENAME="$PACKAGENAME-OSS"
    else
        PACKAGENAME="$PACKAGENAME-Commercial"
    fi

    PACKAGENAME="$PACKAGENAME-$OS"

    if [[ "$distro" != "" ]]; then
        PACKAGENAME="$PACKAGENAME-$distro"
    fi

    PACKAGENAME="$PACKAGENAME-v$VERSIONSTRING"
    PACKAGEDIRECTORY="$OUTPUTDIR/$PACKAGENAME"

    if [[ -d $PACKAGEDIRECTORY ]] ; then
        rm -rf "$PACKAGEDIRECTORY"
    fi
    mkdir "$PACKAGEDIRECTORY"
}

function postCopy {
    local subdir=$1

    pushd "$OUTPUTDIR" &> /dev/null

    tar -zcvf "$PACKAGENAME.tar.gz" "$PACKAGENAME"
    rm -r "$PACKAGEDIRECTORY"

    [[ -d ../../packages/$subdir ]] || mkdir -p ../../packages/$subdir
    mv "$PACKAGENAME.tar.gz" ../../packages/$subdir
    writeLog "Created package: $PACKAGENAME.tar.gz under packages/$subdir"
    popd &> /dev/null

    rm -r "$OUTPUTDIR"
}

preCopy

clusterNodeProject="$baseDir/../../oss/src/EventStore.ClusterNode/EventStore.ClusterNode.csproj"
testClientProject="$baseDir/../../oss/src/EventStore.TestClient/EventStore.TestClient.csproj"
ldapProject="$baseDir/../../src/EventStore.Auth.Ldaps/EventStore.Auth.Ldaps.csproj"

publishProject $clusterNodeProject

if [[ "$BUILD" == "commercial" ]]; then
    mkdir -p $PACKAGEDIRECTORY/plugins/EventStore.Auth.Ldaps
    dotnet publish $ldapProject -c $Configuration -r $targetRunTime -o $PACKAGEDIRECTORY/plugins/EventStore.Auth.Ldaps --no-build /p:Version=$version /p:Platform=x64
fi

publishProject $testClientProject

cp "$baseDir/run-node.sh" "$PACKAGEDIRECTORY/run-node.sh"
mv $PACKAGEDIRECTORY/EventStore.ClusterNode $PACKAGEDIRECTORY/eventstored
mv $PACKAGEDIRECTORY/EventStore.TestClient $PACKAGEDIRECTORY/testclient

postCopy "$distro_shortname"
