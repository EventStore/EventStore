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
# This script is used to build Google V8 and the native Projections library on Linux
# 

set -e

SCRIPT_ROOT=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
EVENTSTORE_ROOT="$SCRIPT_ROOT/../.."
DEPOT_TOOLS_DIRECTORY="$SCRIPT_ROOT/depot_tools"
V8_BUILD_DIRECTORY="$SCRIPT_ROOT/v8"
LLVM_DIRECTORY="$SCRIPT_ROOT/llvm"
LLVM_SOURCE="http://llvm.org/releases/3.8.1"
DEPOT_TOOLS_SOURCE="https://chromium.googlesource.com/chromium/tools/depot_tools.git"
V8_REVISION="branch-heads/5.2"
CONFIGURATION="release"

# shellcheck source=../detect-system/detect-system.sh disable=SC1091
source "$SCRIPT_ROOT/../detect-system/detect-system.sh"

function getDependencies() {
    if [ ! -z "$1" ]; 
        then 
            ARCHIVE_TO_DOWNLOAD=$1
        else 
            ARCHIVE_TO_DOWNLOAD="$LLVM_SOURCE/clang+llvm-3.8.1-x86_64-linux-gnu-$ES_DISTRO-$ES_DISTRO_VERSION.tar.xz"
    fi
    local LLVM_SOURCE=$1
    pushd "$SCRIPT_ROOT" > /dev/null 
        # If LLVM does not exist, download it
        if [[ -d $LLVM_DIRECTORY ]]; 
            then
                echo "LLVM already exists at $LLVM_DIRECTORY."
            else
                echo "Downloading LLVM from $ARCHIVE_TO_DOWNLOAD"
                if wget -q "$ARCHIVE_TO_DOWNLOAD"
                    then 
                        FILE_TO_EXTRACT=$(basename "$ARCHIVE_TO_DOWNLOAD")
                        echo "Extracting $FILE_TO_EXTRACT to $LLVM_DIRECTORY"
                        mkdir -p "$LLVM_DIRECTORY"
                        tar -xf "$FILE_TO_EXTRACT" -C "$LLVM_DIRECTORY" --strip-components=1
                else 
                    echo "Failed to download LLVM. You can supply the URL for the LLVM compiler for your OS as the first argument to this script." 
                    return 1
                fi
        fi

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

function buildv8ProjectionsLibrary() {
    local v8OutputDir="$V8_BUILD_DIRECTORY/out/x64.$CONFIGURATION"

    pushd "$V8_BUILD_DIRECTORY" > /dev/null
        export CLANG_DIR="$LLVM_DIRECTORY"
    	export CXX="$CLANG_DIR/bin/clang++ -std=c++11 -lstdc++"
    	export CC="$CLANG_DIR/bin/clang"
    	export CPP="$CLANG_DIR/bin/clang -E"
    	export LINK="$CLANG_DIR/bin/clang++ -std=c++11 -lstdc++"
    	export CXX_host="$CLANG_DIR/bin/clang++"
    	export CC_host="$CLANG_DIR/bin/clang"
    	export CPP_host="$CLANG_DIR/bin/clang -E"
    	export LINK_host="$CLANG_DIR/bin/clang++"
    	export GYP_DEFINES="clang=1"
    	export CFLAGS="-fPIC"
        export CXXFLAGS="-fPIC"
        if [ ! -z "$1" ];
            then
                export GYPFLAGS="$1"
        fi
        make x64.$CONFIGURATION werror=no
    popd > /dev/null

    local outputDir="$EVENTSTORE_ROOT/src/libs/x64/$ES_DISTRO-$ES_DISTRO_VERSION"
    [[ -d "$outputDir" ]] || mkdir -p "$outputDir"

    pushd "$EVENTSTORE_ROOT/src/EventStore.Projections.v8Integration/" > /dev/null
        local outputObj=$outputDir/libjs1.so
        local v8Libs=(
            "$v8OutputDir/obj.target/third_party/icu/libicui18n.a"
            "$v8OutputDir/obj.target/third_party/icu/libicuuc.a"
            "$v8OutputDir/obj.target/third_party/icu/libicudata.a"
            "$v8OutputDir/obj.target/src/libv8_base.a"
            "$v8OutputDir/obj.target/src/libv8_libbase.a"
            "$v8OutputDir/obj.target/src/libv8_libplatform.a"
            "$v8OutputDir/obj.target/src/libv8_nosnapshot.a")

        "$CLANG_DIR/bin/clang++" -g -I "$V8_BUILD_DIRECTORY/include" -I "$V8_BUILD_DIRECTORY/include/libplatform" *.cpp -m64 -o "$outputObj" -fPIC -shared -std=c++11 -lstdc++ -Wl,--start-group $(joinBy " " "${v8Libs[@]}") -Wl,--end-group -lrt -lpthread
        echo "Output: $(readlink -f "$outputObj")"
    popd > /dev/null
}

getSystemInformation
if [ "$ES_DISTRO" == "osx" ]; then
    echo "This script is not intended for use on Mac OS X - please use the script named build-js1-mac.sh instead"
    exit 1
fi

getDependencies "$1"
getV8 $V8_REVISION
buildv8ProjectionsLibrary "$2"