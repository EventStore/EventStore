#!/bin/bash
#------------ Start of configuration -------------

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

V8_TAG="3.19.7"

#------------ End of configuration -------------

function usage() {
    echo ""
    echo "Usage: $0 action <platform=x64> <configuration=release>"
    echo ""
    echo "Valid actions are:"
    echo "  quick - assumes libjs1.so and libv8.so are available and"
    echo "          fails if this is not the case."
    echo ""
    echo "  incremental - always rebuilds libjs1.so, but will only"
    echo "                build V8 if libv8.so is not available."
    echo ""
    echo "  full - will clean the repository prior to building. This"
    echo "         always builds libv8.so and libjs1.so."
    echo ""
    echo "Valid platforms are:"
    echo "  x64"
    echo "  x86"
    echo ""
    echo "Valid configurations are:"
    echo "  debug"
    echo "  release"
    echo ""
    exit 1
}

ACTION=""
PLATFORM="x64"
CONFIGURATION="Release"

function checkParams() {
    action=$1
    platform=$2
    configuration=$3

    if [[ "$action" == "quick" || "$action" == "incremental" || "$action" == "full" ]]; then
        ACTION=$action
        echo "Action set to: $ACTION"
    else
        echo "Invalid action: $action"
        usage
    fi

    if [[ "$platform" == "" ]]; then
        PLATFORM="x64"
        echo "Platform defaulted to: $PLATFORM"
    else
        if [[ "$platform" == "x64" || "$platform" == "x86" ]]; then
            PLATFORM=$platform
            echo "Platform set to: $PLATFORM"
        else
            echo "Invalid platform: $platform"
            usage
        fi
    fi

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
}

function err() {
    echo "FAILED. See earlier messages"
    exit 1
}

function get-v8() {
    tag=$1

    if [[ -d v8 ]] ; then
        git pull || err
    else
        git clone git://github.com/v8/v8.git v8 || err
    fi
    pushd v8 || err
    git checkout $tag || err
    popd || err
}

function get-dependencies() {
    pushd v8 || err
    make dependencies || err
    popd || err
}


[[ $# -eq 0 || $# -gt 3 ]] && usage

echo "Running from base directory: $BASE_DIR"

checkParams $1 $2 $3

get-v8 $V8_TAG
get-dependencies

