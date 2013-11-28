#!/usr/bin/env bash
#------------ Start of configuration -------------

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
V8_REVISION="17915" #Tag 3.22.6
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store LLP"
COPYRIGHT="Copyright 2012 Event Store LLP. All rights reserved."

make='make'
if [[ `uname` == 'Linux' ]]; then
    make='make'
elif [[ `uname` == 'FreeBSD' ]]; then
    make='gmake'
elif [[ `uname` == 'Darwin' ]]; then
    make='make'
fi

#------------ End of configuration -------------

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

function err() {
    revertVersionFiles
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
    if [[ -d v8/build/gyp ]] ; then
        pushd v8/build/gyp > /dev/null || err
        svnrevision=`svn info | sed -ne 's/^Revision: //p'`
        popd > /dev/null || err
    fi

    pushd v8 > /dev/null || err
    if [[ "$svnrevision" -ne "1501" ]] ; then
        $make dependencies || err
    else
        echo "GYP already up to date (r $svnrevision)"
    fi
    popd > /dev/null || err
}

function buildV8() {
    pushd v8 > /dev/null || err
   
    if [[ "$PLATFORM" -eq "x64" ]] ; then
        makecall="x64.$CONFIGURATION"
    elif [[ "$PLATFORM" -eq "x86" ]] ; then
        makecall="ia32.$CONFIGURATION"
    else
        echo "Unsupported platform $PLATFORM."
        exit 1
    fi

    $make $makecall $WERRORSTRING library=shared || err

    pushd out/$makecall/lib.target > /dev/null
    cp libv8.so ../../../../src/EventStore/libs || err
    cp libicui18n.so ../../../../src/EventStore/libs || err
    cp libicuuc.so ../../../../src/EventStore/libs || err
    popd > /dev/null

    [[ -d ../src/EventStore/libs/include ]] || mkdir ../src/EventStore/libs/include

    pushd include > /dev/null || err
    cp *.h ../../src/EventStore/libs/include || err
    popd > /dev/null || err

    popd > /dev/null || err
}

function buildJS1() {
    currentDir=$(pwd -P)
    includeString="-I $currentDir/src/EventStore/libs/include"
    libsString="-L $currentDir/src/EventStore/libs"
    outputDir="$currentDir/src/EventStore/libs"

    pushd $currentDir/src/EventStore/EventStore.Projections.v8Integration/ > /dev/null || err

    if [[ "$ARCHITECTURE" == "x86" ]] ; then
        gccArch="-arch i386"
    elif [[ "$ARCHITECTURE" == "x64" ]] ; then
        gccArch="-arch amd64"
    fi

    g++ $includeString $libsString *.cpp -o $outputDir/libjs1.so $gccArch -lv8 -O2 -fPIC --shared --save-temps -std=c++0x || err
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

function buildEventStore {
    patchVersionFiles
    rm -rf bin/
    xbuild src/EventStore/EventStore.sln /p:Platform="Any CPU" /p:Configuration="$CONFIGURATION" || err
    revertVersionFiles
}

function cleanAll {
    rm -rf bin/
    rm -f src/EventStore/libs/libv8.so
    rm -f src/EventStore/libs/libjs1.so
    pushd src/EventStore/EventStore.Projections.v8Integration > /dev/null
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
        [[ -f src/EventStore/libs/libv8.so ]] || exitWithError "Cannot find libv8.so - cannot do a quick build!"
        [[ -f src/EventStore/libs/libjs1.so ]] || exitWithError "Cannot find libjs1.so - cannot do a quick build!"

        buildEventStore
    fi
fi
