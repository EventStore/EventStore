#!/usr/bin/env bash

SCRIPT_ROOT=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
EVENTSTORE_ROOT="$SCRIPT_ROOT/../.."
V8_BUILD_DIRECTORY="$SCRIPT_ROOT/v8"
V8_REVISION="18454" #Tag 3.24.10
CONFIGURATION="release"

# shellcheck source=../detect-system/detect-system.sh disable=SC1091
source $SCRIPT_ROOT/../detect-system/detect-system.sh

# Replicate readlink -f on Mac OS X because BSD...
function abspath() {
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
        GYP_DEFINES="clang=1 mac_deployment_target=10.9" \
        CFLAGS="-fPIC" \
        CXXFLAGS="-fPIC" \
        make x64.$CONFIGURATION werror=no
    popd > /dev/null

    local outputDir="$EVENTSTORE_ROOT/src/libs/x64/$ES_DISTRO-$ES_DISTRO_VERSION"
    [[ -d "$outputDir" ]] || mkdir -p "$outputDir"

    pushd "$EVENTSTORE_ROOT/src/EventStore.Projections.v8Integration/" > /dev/null

    local outputObj=$outputDir/libjs1.so

    local libsString="$v8OutputDir/libicudata.a \
        $v8OutputDir/libicui18n.a \
        $v8OutputDir/libicuuc.a \
        $v8OutputDir/libv8_base.x64.a \
        $v8OutputDir/libv8_nosnapshot.x64.a \
        $v8OutputDir/libv8_snapshot.a"
    g++ -I "$V8_BUILD_DIRECTORY/include" "$libsString" ./*.cpp -o "$outputObj" -O2 -fPIC --shared --save-temps -std=c++0x
    install_name_tool -id libjs1.dylib "$outputObj"
    echo "Output: $(abspath "$outputObj")"

    popd > /dev/null
}

getSystemInformation
set -e
if [ "$ES_DISTRO" != "osx" ]; then
    echo "This script is only intended for use on Mac OS X - please use the script named build-js1-linux.sh instead"
    exit 1
fi
get_v8_and_dependencies $V8_REVISION
build_js1
