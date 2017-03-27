#!/usr/bin/env bash

# File License:
#     Copyright 2016, Event Store LLP
#
#     Licensed under the Apache License, Version 2.0 (the "License");
#     you may not use this file except in compliance with the License.
#     You may obtain a copy of the License at
#
#         http://www.apache.org/licenses/LICENSE-2.0
#
#     Unless required by applicable law or agreed to in writing, software
#     distributed under the License is distributed on an "AS IS" BASIS,
#     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
#     See the License for the specific language governing permissions and
#     limitations under the License.
#
# This script is used to build Google V8 and the native Projections library on OSX
# 

SCRIPT_ROOT=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
EVENTSTORE_ROOT="$SCRIPT_ROOT/../.."
DEPOT_TOOLS_DIRECTORY="$SCRIPT_ROOT/depot_tools"
V8_BUILD_DIRECTORY="$SCRIPT_ROOT/v8"
V8_REVISION="branch-heads/5.2"
CONFIGURATION="release"
DEPOT_TOOLS_SOURCE="https://chromium.googlesource.com/chromium/tools/depot_tools.git"

# shellcheck source=../detect-system/detect-system.sh disable=SC1091
source "$SCRIPT_ROOT/../detect-system/detect-system.sh"

# Replicate readlink -f on Mac OS X because BSD...
function absolutePath() {
    pushd . > /dev/null
        if [ -d "$1" ]; then
            cd "$1"
            dirs -l +0
        else
            cd "$(dirname "$1")"
            cur_dir=$(dirs -l +0)
            if [ "$cur_dir" == "/" ]; then
                echo "$cur_dir$(basename "$1")"
            else
                echo "$cur_dir/$(basename "$1")"
            fi
        fi
    popd > /dev/null;
}

function getDependencies() {
    pushd "$SCRIPT_ROOT" > /dev/null 
        # Depot Tools is required for checking out and updating dependencies for v8
        if [[ -d $DEPOT_TOOLS_DIRECTORY ]];
            then
                echo "Depot Tools already exists at $DEPOT_TOOLS_DIRECTORY."
            else
                echo "Downloading Depot Tools (required for getting v8 and dependencies)"
                if ! git clone $DEPOT_TOOLS_SOURCE $DEPOT_TOOLS_DIRECTORY
                    then
                        echo "Failed to clone $DEPOT_TOOLS_SOURCE, cannot continue"
                        return 1
                fi
        fi
    popd > /dev/null
    echo "Placing $DEPOT_TOOLS_DIRECTORY in Path (Required to fetch v8 and sync dependencies)"
    export PATH=$DEPOT_TOOLS_DIRECTORY:$PATH
}

function getV8() {
    local revision=$1

    if [[ -d $V8_BUILD_DIRECTORY ]]; then
        echo "V8 already exists at $V8_BUILD_DIRECTORY."
        pushd "$V8_BUILD_DIRECTORY" > /dev/null 
            git reset --hard
            git checkout "$revision"
            gclient sync
        popd > /dev/null
    else
        echo "Fetching v8..."
        pushd "$SCRIPT_ROOT" > /dev/null 
            fetch v8
        popd > /dev/null
        pushd "$V8_BUILD_DIRECTORY" > /dev/null 
            git reset --hard
            git checkout "$revision"
            gclient sync
        popd > /dev/null
    fi
}

function joinBy() { 
    local IFS="$1"; shift; echo "$*"; 
}

function buildv8ProjectionsLibrary() {
    local v8OutputDir="$V8_BUILD_DIRECTORY/out/x64.$CONFIGURATION"

    pushd "$V8_BUILD_DIRECTORY" > /dev/null 
        CXX=$(which clang++) \
        CC=$(which clang) \
        CPP="$(which clang) -E -std=c++0x -stdlib=libc++" \
        LINK="$(which clang++) -std=c++0x -stdlib=libc++" \
        CXX_host=$(which clang++) \
        CC_host=$(which clang) \
        CPP_host="$(which clang) -E" \
        LINK_host=$(which clang++) \
        GYP_DEFINES="clang=1 mac_deployment_target=10.11" \
        CFLAGS="-fPIC" \
        CXXFLAGS="-fPIC" \
        GYPFLAGS="-Dv8_use_external_startup_data=0" \
        make x64.$CONFIGURATION werror=no
    popd > /dev/null

    local outputDir="$EVENTSTORE_ROOT/src/libs/x64/$ES_DISTRO-$ES_DISTRO_VERSION"
    [[ -d "$outputDir" ]] || mkdir -p "$outputDir"

    pushd "$EVENTSTORE_ROOT/src/EventStore.Projections.v8Integration/" > /dev/null
        local outputObj=$outputDir/libjs1.dylib
        local v8Libs=(
            "$v8OutputDir/libicudata.a"
            "$v8OutputDir/libicui18n.a"
            "$v8OutputDir/libicuuc.a"
            "$v8OutputDir/libv8_base.a"
            "$v8OutputDir/libv8_libbase.a"
            "$v8OutputDir/libv8_libplatform.a"
            "$v8OutputDir/libv8_nosnapshot.a")

        g++ -I "$V8_BUILD_DIRECTORY/include" -I "$V8_BUILD_DIRECTORY/include/libplatform" $(joinBy " " "${v8Libs[@]}") ./*.cpp -o "$outputObj" --shared -O2 -fPIC -lpthread -lstdc++ -std=c++0x
        install_name_tool -id libjs1.dylib "$outputObj"
        echo "Output: $(absolutePath "$outputObj")"
    popd > /dev/null
}

getSystemInformation
set -e
if [ "$ES_DISTRO" != "osx" ]; then
    echo "This script is only intended for use on Mac OS X - please use the script named build-js1-linux.sh instead"
    exit 1
fi

getDependencies
getV8 $V8_REVISION
buildv8ProjectionsLibrary
