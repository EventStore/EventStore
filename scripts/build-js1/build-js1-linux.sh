#!/usr/bin/env bash

set -e

SCRIPT_ROOT=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
EVENTSTORE_ROOT="$SCRIPT_ROOT/../.."
V8_BUILD_DIRECTORY="$SCRIPT_ROOT/v8"
V8_REVISION="18454" #Tag 3.24.10
CONFIGURATION="release"

# shellcheck source=../detect-system/detect-system.sh disable=SC1091
source "$SCRIPT_ROOT/../detect-system/detect-system.sh"

function get_v8_and_dependencies() {
    local revision=$1

    if [[ -d $V8_BUILD_DIRECTORY ]]; then
        echo "There is already a directory present at $V8_BUILD_DIRECTORY."

        pushd "$V8_BUILD_DIRECTORY" > /dev/null 
        echo "Updating V8 repository to revision $revision..."
        svn update --quiet -r"$revision"
        if [ "$?" != "0" ]; then
            echo "Cannot update existing V8 directory at $V8_BUILD_DIRECTORY to revision $revision, is it a valid checkout?"
            exit 1
        fi
        popd > /dev/null 
    else
        echo "Checking out V8 repository..."
        svn checkout --quiet -r"$revision" https://v8.googlecode.com/svn/trunk "$V8_BUILD_DIRECTORY"
    fi

    local needsDependencies=false

    if [[ -d $V8_BUILD_DIRECTORY/build/gyp ]] ; then
        pushd "$V8_BUILD_DIRECTORY/build/gyp" > /dev/null
        currentGypRevision=$(svn info | sed -ne 's/^Revision: //p')
        if [[ "$currentGypRevision" -ne "1806" ]] ; then
            needsDependencies=true
        fi
        popd > /dev/null
    else
        needsDependencies=true
    fi

    if [[ -d $V8_BUILD_DIRECTORY/third_party/icu ]] ; then
        pushd "$V8_BUILD_DIRECTORY/third_party/icu" > /dev/null
        currentIcuRevision=$(svn info | sed -ne 's/^Revision: //p')
        if [[ "$currentIcuRevision" -ne "239289" ]] ; then
            needsDependencies=true
        fi
        popd > /dev/null
    else
        needsDependencies=true
    fi

    if $needsDependencies ; then
        pushd "$V8_BUILD_DIRECTORY" > /dev/null 
        echo "Running make dependencies"
        make dependencies 
        popd > /dev/null 
    else
        echo "Dependencies already at correct revisions"
    fi
}

function build_js1() {
    local WERROR_STRING=$1
    local v8OutputDir="$V8_BUILD_DIRECTORY/out/x64.$CONFIGURATION/obj.target"

    if [ "$WERROR_STRING" != "" ] && [ "$WERROR_STRING" != "werror=no" ]; then
        echo "The only argument present must be \"werror=no\""
        exit 1
    fi

    pushd "$V8_BUILD_DIRECTORY" > /dev/null 
    pushd "$V8_BUILD_DIRECTORY" > /dev/null
    CFLAGS="-fPIC" CXXFLAGS="-fPIC" make x64.$CONFIGURATION "$WERROR_STRING"
    popd > /dev/null

    local outputDir="$EVENTSTORE_ROOT/src/libs/x64/$ES_DISTRO-$ES_DISTRO_VERSION"
    [[ -d "$outputDir" ]] || mkdir -p "$outputDir"

    pushd "$EVENTSTORE_ROOT/src/EventStore.Projections.v8Integration/" > /dev/null

    local outputObj=$outputDir/libjs1.so

    g++ -I "$V8_BUILD_DIRECTORY/include *.cpp" -o "$outputObj" -fPIC -shared -std=c++0x -lstdc++ -Wl,--start-group "$v8OutputDir/tools/gyp/libv8_base.x64.a" "$v8OutputDir/tools/gyp/libv8_nosnapshot.x64.a" "$v8OutputDir/third_party/icu/libicui18n.a" "$v8OutputDir/third_party/icu/libicuuc.a" "$v8OutputDir/third_party/icu/libicudata.a" -Wl,--end-group -lrt -lpthread 
    echo "Output: $(readlink -f "$outputObj")"

    popd > /dev/null
}

getSystemInformation
if [ "$ES_DISTRO" == "osx" ]; then
    echo "This script is not intended for use on Mac OS X - please use the script named build-js1-mac.sh instead"
    exit 1
fi
get_v8_and_dependencies $V8_REVISION
build_js1 "$1"
