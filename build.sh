#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
V8_REVISION="18454" #Tag 3.24.10
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store LLP"
COPYRIGHT="Copyright 2012 Event Store LLP. All rights reserved."

# NOTE: We detect whether we're running on a Mac-like system
# because in that case we need to deal with .dylib's instead
# of .so's, and set the DYLD_LIBRARY_PATH before attempting
# to build V8's snapshot. The output paths also appear to be
# different.

# TODO: Figure out whether FreeBSD behaves like MacOS or like
# Linux.
platform='unix'
make='make'
if [[ `uname` == 'Linux' ]]; then
    make='make'
    unixtype='unix'
elif [[ `uname` == 'FreeBSD' ]]; then
    # TODO: Does FreeBSD behave like OS X or Linux?
    make='gmake'
    unixtype='unix'
elif [[ `uname` == 'Darwin' ]]; then
    make='make'
    unixtype='mac'
fi

# Ensure we have dependencies
type gcc >/dev/null 2>&1 || { echo >&2 "Building requires GCC (gcc) on the path. Aborting."; exit 1; }
type xbuild >/dev/null 2>&1 || { echo >&2 "Building requires XBuild (xbuild) from Mono on the path. Aborting."; exit 1; }
type svn >/dev/null 2>&1 || { echo >&2 "Building requires Subversion (svn) on the path. Aborting."; exit 1; }
type git >/dev/null 2>&1 || { echo >&2 "Building requires Git (git) on the path. Aborting."; exit 1; }
pkgoutput=`pkg-config --cflags monosgen-2`
[[ "$?" -eq 0 ]] || { echo >&2 "Packaging requires monosgen-2 to be discoverable via pkg-config. Consider setting \$PKG_CONFIG_PATH to the /lib/pkgconfig subdirectory of your Mono installation. Aborting."; exit 1; }
# ------------ End of configuration -------------

function usage() {
    echo ""
    echo "Usage: $0 action <version=0.0.0.0> <platform=x64> <configuration=release> [no-werror]"
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
    echo "  js1 -  rebuild libjs1.lib only"
    echo ""
    echo "Valid platforms are:"
    echo "  x86"
    echo "  x64"
    echo ""
    echo "Valid configurations are:"
    echo "  debug"
    echo "  release"
    echo ""
    echo "Pass no-werror to pass werror=no to V8 make (for newer GCC builds)"
    exit 1
}

ACTION="quick"
PLATFORM="x64"
CONFIGURATION="Release"
WERRORSTRING=""

function checkParams() {
    action=$1
    version=$2
    platform=$3
    configuration=$4
    nowerror=$5

    [[ $# -gt 5 ]] && usage

    if [[ "$action" = "" ]]; then
        ACTION="quick"
        echo "Action defaulted to: $ACTION"
    else
        if [[ "$action" == "quick" || "$action" == "incremental" || "$action" == "full" || "$action" == "js1" ]]; then
            ACTION=$action
            echo "Action set to: $ACTION"
        else
            echo "Invalid action: $action"
            usage
        fi
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

    if [[ "$version" == "" ]] ; then
        VERSIONSTRING="0.0.0.0"
        echo "Version defaulted to: 0.0.0.0"
    else
        VERSIONSTRING=$version
        echo "Version set to: $VERSIONSTRING"
    fi

	if [[ "$nowerror" == "no-werror" ]] ; then
		WERRORSTRING="werror=no"
		echo "Setting werror=no for V8 build"
	fi
}

function revertVersionFiles() {
    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        git checkout $file
        echo "Reverted $file"
    done
}

function revertVersionInfo() {
    files=$( find . -name "VersionInfo.cs" )

    for file in $files
    do
        git checkout $file
        echo "Reverted $file"
    done
}

function err() {
    revertVersionFiles
    revertVersionInfo
    echo "FAILED. See earlier messages"
    exit 1
}

function getV8() {
    revision=$1

    if [[ -d v8/.svn ]] ; then
        pushd v8 > /dev/null || err
		svnrevision=`svn info | sed -ne 's/^Revision: //p'`

        if [[ "$svnrevision" != "$revision" ]] ; then
            echo "Updating V8 repository to revision $revision..."
            svn update --quiet -r$revision
		else
            echo "V8 repository already at revision $revision"
        fi
        popd > /dev/null || err
    else
		if [[ -d v8 ]] ; then
			echo
		fi
		echo "Checking out V8 repository..."
		svn checkout --quiet -r$revision http://v8.googlecode.com/svn/trunk v8
    fi
}

function getDependencies() {
    needsDependencies=false

    if [[ -d v8/build/gyp ]] ; then
        pushd v8/build/gyp > /dev/null || err
        currentGypRevision=`svn info | sed -ne 's/^Revision: //p'`
        if [[ "$currentGypRevision" -ne "1806" ]] ; then
            needsDependencies=true
        fi
        popd > /dev/null || err
    else
        needsDependencies=true
    fi

    if [[ -d v8/third_party/icu ]] ; then
        pushd v8/third_party/icu > /dev/null || err
        currentIcuRevision=`svn info | sed -ne 's/^Revision: //p'`
        if [[ "$currentIcuRevision" -ne "239289" ]] ; then
            needsDependencies=true
        fi
        popd > /dev/null || err
    else
        needsDependencies=true
    fi

    if $needsDependencies ; then
        pushd v8 > /dev/null || err
        echo "Running make dependencies"
        $make dependencies || err
        popd > /dev/null || err
    else
        echo "Dependencies already at correct revisions"
    fi
}

function buildV8() {
    pushd v8 > /dev/null || err

    if [[ "$PLATFORM" == "x64" ]] ; then
        makecall="x64.$CONFIGURATION"
    elif [[ "$PLATFORM" == "x86" ]] ; then
        makecall="ia32.$CONFIGURATION"
    else
        echo "Unsupported platform $PLATFORM."
        exit 1
    fi

    if [[ "$unixtype" == "mac" ]] ; then
        v8OutputDir=`pwd`/out/$makecall
        fileext="dylib"
        DYLD_LIBRARY_PATH=$v8OutputDir $make $makecall $WERRORSTRING library=shared || err
    else
        v8OutputDir=`pwd`/out/$makecall/lib.target
        fileext="so"
        $make $makecall $WERRORSTRING library=shared || err
    fi

    echo "Coping some crap" $fileext
    pushd ../src/libs > /dev/null
    cp $v8OutputDir/libv8.$fileext . || err
    cp $v8OutputDir/libicui18n.$fileext . ||  err
    cp $v8OutputDir/libicuuc.$fileext . || err

    if [[ "$unixtype" == "mac" ]] ; then
        install_name_tool -id libv8.dylib libv8.dylib
        install_name_tool -id libicui18n.dylib libicui18n.dylib
        install_name_tool -id libicuuc.dylib libicuuc.dylib
        install_name_tool -change /usr/local/lib/libicuuc.dylib libicuuc.dylib libicui18n.dylib
        install_name_tool -change /usr/local/lib/libicuuc.dylib libicuuc.dylib libv8.dylib
        install_name_tool -change /usr/local/lib/libicui18n.dylib libicui18n.dylib libv8.dylib
    fi
    popd > /dev/null

    [[ -d ../src/libs/include ]] || mkdir ../src/libs/include

    pushd include > /dev/null || err
    cp *.h ../../src/libs/include || err
    popd > /dev/null || err

    popd > /dev/null || err
}

function buildJS1() {
    currentDir=$(pwd -P)
    includeString="-I $currentDir/src/libs/include"
    libsString="-L $currentDir/src/libs"
    outputDir="$currentDir/src/libs"

    pushd $currentDir/src/EventStore.Projections.v8Integration/ > /dev/null || err

    if [[ "$ARCHITECTURE" == "x86" ]] ; then
        gccArch="-arch i386"
    elif [[ "$ARCHITECTURE" == "x64" ]] ; then
        gccArch="-arch amd64"
    fi

    if [[ "$unixtype" == "mac" ]] ; then
        outputObj=$outputDir/libjs1.dylib
    else
        outputObj=$outputDir/libjs1.so
    fi

    g++ $includeString $libsString *.cpp -o $outputObj $gccArch -lv8 -O2 -fPIC --shared --save-temps -std=c++0x || err

    if [[ "$unixtype" == "mac" ]] ; then
        pushd $outputDir > /dev/null || err
        install_name_tool -id libjs1.dylib libjs1.dylib
        install_name_tool -change /usr/local/lib/libv8.dylib libv8.dylib libjs1.dylib
        popd > /dev/null || err
    fi


    popd > /dev/null || err
}

function patchVersionFiles {
    branchName=`git rev-parse --abbrev-ref HEAD`
    commitHashAndTime=`git log -n1 --pretty=format:"%H@%aD" HEAD`

    newAssemblyVersion="[assembly: AssemblyVersion(\"$VERSIONSTRING\")]"
    newAssemblyFileVersion="[assembly: AssemblyFileVersion(\"$VERSIONSTRING\")]"
    newAssemblyVersionInformational="[assembly: AssemblyInformationalVersion(\"$VERSIONSTRING.$branchName@$commitHashAndTime\")]"
    newAssemblyProductName="[assembly: AssemblyProduct(\"$PRODUCTNAME\")]"
    newAssemblyCopyright="[assembly: AssemblyCopyright(\"$COPYRIGHT\")]"
    newAssemblyCompany="[assembly: AssemblyCompany(\"$COMPANYNAME\")]"

    assemblyVersionPattern='AssemblyVersion(.*'
    assemblyFileVersionPattern='AssemblyFileVersion(.*'
    assemblyVersionInformationalPattern='AssemblyInformationalVersion(.*'
    assemblyProductNamePattern='AssemblyProduct(.*'
    assemblyCopyrightPattern='AssemblyCopyright(.*'
    assemblyCompanyPattern='AssemblyCompany(.*'

    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        tempfile="$file.tmp"
        sed -e '/$assemblyVersionPattern/c\'$'\n''$newAssemblyVersion' \
            -e '/$assemblyFileVersionPattern/c\'$'\n''$newAssemblyFileVersion' \
            -e '/$assemblyVersionInformationalPattern/c\'$'\n''$newAssemblyVersionInformational' \
            -e '/$assemblyProductNamePattern/c\'$'\n''$newAssemblyProductName' \
            -e '/$assemblyCopyrightPattern/c\'$'\n''$newAssemblyCopyright' \
            -e '/$assemblyCompanyPattern/c\'$'\n''$newAssemblyCompany' \
            $file > $tempfile || err

        mv $tempfile $file

        if grep "AssemblyInformationalVersion" $file > /dev/null ; then
            echo "Patched $file with version information"
        else
            echo " " >> $file
            echo $newAssemblyVersionInformational >> $file
            echo "Patched $file with version information"
        fi
    done
}

function patchVersionInfo {
    branchName=`git rev-parse --abbrev-ref HEAD`
    commitHash=`git log -n1 --pretty=format:"%H" HEAD`
    commitTimestamp=`git log -n1 --pretty=format:"%aD" HEAD`

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
        $file > $tempfile

        mv $tempfile $file
	echo "Patched $file with version information"
    done
}

function buildEventStore {
    patchVersionFiles
    patchVersionInfo
    rm -rf bin/
    xbuild src/EventStore.sln /p:Platform="Any CPU" /p:Configuration="$CONFIGURATION" || err
    revertVersionFiles
    revertVersionInfo
}

function cleanAll {
    rm -rf bin/
    rm -f src/libs/libv8.so
    rm -f src/libs/libjs1.so
    pushd src/EventStore.Projections.v8Integration > /dev/null
    git clean --quiet -dfx -- .
    popd > /dev/null
}

function exitWithError {
    echo $1
    exit 1
}

checkParams $1 $2 $3 $4 $5

echo "Running from base directory: $BASE_DIR"

if [[ "$ACTION" == "full" ]] ; then
    cleanAll
fi

if [[ "$ACTION" == "js1" ]] ; then
    buildJS1
else

    if [[ "$ACTION" == "incremental" || "$ACTION" == "full" ]] ; then
        getV8 $V8_REVISION
        getDependencies

        buildV8
        buildJS1
        buildEventStore
    else
        [[ -f src/libs/libv8.so ]] || [[ -f src/libs/libv8.dylib ]] || exitWithError "Cannot find libv8.[so|dylib] - in src/libs/ so cannot do a quick build!"
        [[ -f src/libs/libicui18n.so ]] || [[ -f src/libs/libicui18n.dylib ]] || exitWithError "Cannot find libicui18n.[so|dylib] - in src/libs/ so cannot do a quick build!"
        [[ -f src/libs/libjs1.so ]] || [[ -f src/libs/libjs1.dylib ]] || exitWithError "Cannot find libjs1.[so|dylib] - at src/libs/ so cannot do a quick build!"

        buildEventStore
    fi
fi
