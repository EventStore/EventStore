#!/usr/bin/env bash

# File License:
#     Copyright 2018, Event Store LLP
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
# This script is used to build Google V8 and the native Projections library for Mac OS X 10.9+
#

set -e

SCRIPT_ROOT=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

EVENTSTORE_SOURCE="https://github.com/EventStore/EventStore.git"
EVENTSTORE_BRANCH="master"

DEPOT_TOOLS_DIRECTORY="$SCRIPT_ROOT/depot_tools"
DEPOT_TOOLS_SOURCE="https://chromium.googlesource.com/chromium/tools/depot_tools.git"

V8_BUILD_DIRECTORY="$SCRIPT_ROOT/v8"
V8_REVISION="branch-heads/7.0"
V8_CONFIGURATION="release"

function getDependencies() {
    pushd "$SCRIPT_ROOT" > /dev/null
        # Depot Tools is required for checking out and updating dependencies for v8
        if [[ -d $DEPOT_TOOLS_DIRECTORY ]];
            then
                echo "Depot Tools already exists at $DEPOT_TOOLS_DIRECTORY."
            else
                echo "Placing $DEPOT_TOOLS_DIRECTORY in Path (Required to fetch v8 and sync dependencies)"
                if ! git clone $DEPOT_TOOLS_SOURCE $DEPOT_TOOLS_DIRECTORY
                    then
                        echo "Failed to clone $DEPOT_TOOLS_SOURCE, cannot continue"
                        return 1
                fi
        fi
    popd > /dev/null
    echo "Placing $DEPOT_TOOLS_DIRECTORY in Path (Required to run gclient)"
    export PATH=$DEPOT_TOOLS_DIRECTORY:$PATH
}

function getV8() {
    local revision=$1

    if [[ -d $V8_BUILD_DIRECTORY ]]; then
        echo "$V8_BUILD_DIRECTORY already exists, syncing..."
        pushd "$V8_BUILD_DIRECTORY" > /dev/null
            git reset --hard
            git checkout "$revision"
        popd > /dev/null
        pushd "$SCRIPT_ROOT" > /dev/null
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
        popd > /dev/null
    fi
}

function joinBy() {
    local IFS="$1"; shift; echo "$*";
}

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

function buildv8ProjectionsLibrary() {
    local v8OutputDir="$V8_BUILD_DIRECTORY/out.gn/x64.$V8_CONFIGURATION"

    pushd "$V8_BUILD_DIRECTORY" > /dev/null
        ./tools/dev/v8gen.py -vv x64."$V8_CONFIGURATION" -- v8_monolithic=true v8_use_external_startup_data=false use_sysroot=false use_cxx11=true use_pic=true use_custom_libcxx=true use_custom_libcxx_for_host=true treat_warnings_as_errors=true mac_sdk_min=\"10.9\"
        ninja -C "$v8OutputDir"
    popd > /dev/null

    EVENTSTORE_DIRECTORY="$SCRIPT_ROOT/EventStore"
    if [[ ! -d $EVENTSTORE_DIRECTORY ]]; then
        pushd "$SCRIPT_ROOT" > /dev/null
            git clone "$EVENTSTORE_SOURCE"

            pushd "$EVENTSTORE_DIRECTORY" > /dev/null
                git checkout "$EVENTSTORE_BRANCH"
                git reset --hard
            popd > /dev/null
        popd > /dev/null
    fi

    local outputDir="$SCRIPT_ROOT/"
    [[ -d "$outputDir" ]] || mkdir -p "$outputDir"

    CLANG_DIR="$V8_BUILD_DIRECTORY/third_party/llvm-build/Release+Asserts/"
    V8_INCLUDE_DIR="$V8_BUILD_DIRECTORY/include"
    V8_LIB_DIR="$v8OutputDir/obj/"
    LIBCXX_INCLUDE_DIR="$V8_BUILD_DIRECTORY/third_party/llvm-build/Release+Asserts/include/c++/v1/"

    pushd "$EVENTSTORE_DIRECTORY/src/EventStore.Projections.v8Integration/" > /dev/null
        local outputObj=$outputDir/libjs1.dylib
        "$CLANG_DIR/bin/clang++" -g -I "$V8_INCLUDE_DIR" -I "$LIBCXX_INCLUDE_DIR" *.cpp -m64 -o "$outputObj" -fPIC -shared -std=c++11 -stdlib=libc++ -lc++ -lv8_monolith -L "$V8_LIB_DIR" -lpthread -mmacosx-version-min=10.9
        install_name_tool -id libjs1.dylib "$outputObj"
        echo "Output: $(absolutePath "$outputObj")"
    popd > /dev/null
    echo "Done!"
}

getDependencies
getV8 $V8_REVISION
buildv8ProjectionsLibrary