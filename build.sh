#------------ Start of configuration -------------

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
V8_TAG="3.19.7"
VERSIONSTRING="0.0.0.0"
PRODUCTNAME="Event Store Open Source"
COMPANYNAME="Event Store LLP"
COPYRIGHT="Copyright 2012 Event Store LLP. All rights reserved."
#------------ End of configuration -------------

function usage() {
    echo ""
    echo "Usage: $0 action <version=0.0.0.0> <platform=x64> <configuration=release>"
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
    echo "  x86"
    echo "  x64"
    echo ""
    echo "Valid configurations are:"
    echo "  debug"
    echo "  release"
    echo ""
    exit 1
}

ACTION="quick"
PLATFORM="x64"
CONFIGURATION="Release"

function checkParams() {
    action=$1
    version=$2
    platform=$3
    configuration=$4
    
    [[ $# -gt 4 ]] && usage

    if [[ "$action" = "" ]]; then
        ACTION="quick"
        echo "Action defaulted to: $ACTION"
    else
        if [[ "$action" == "quick" || "$action" == "incremental" || "$action" == "full" ]]; then
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
        VERSION="0.0.0.0"
        echo "Version defaulted to: 0.0.0.0"
    else
        VERSION=$version
        echo "Version set to: $VERSION"
    fi
}

function revert-versionfiles() {
    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        git checkout $file
        echo "Reverted $file"
    done
}

function err() {
    revert-versionfiles
    echo "FAILED. See earlier messages"
    exit 1
}

function get-v8() {
    tag=$1

    if [[ -d v8 ]] ; then
        pushd v8 > /dev/null || err
        gittag=`git describe --contains HEAD`

        if [[ "$gittag" != "$tag" ]] ; then
            echo "Pulling V8 repository..."
            git pull || err
            echo "Checking out $tag..."
            git checkout $tag || err
        else
            echo "V8 repository already at $gittag"
        fi
        popd > /dev/null || err
    else
        echo "Cloning V8 repository..."
        git clone git://github.com/v8/v8.git v8 || err
        pushd v8 > /dev/null || err
        echo "Checking out $tag..."
        git checkout $tag
        popd > /dev/null || err
    fi
}

function get-dependencies() {
    if [[ -d v8/build/gyp ]] ; then
        pushd v8/build/gyp > /dev/null || err
        svnrevision=`svn info | sed -ne 's/^Revision: //p'`
        popd > /dev/null || err
    fi

    pushd v8 > /dev/null || err
    if [[ "$svnrevision" -ne "1501" ]] ; then
        make dependencies || err
    else
        echo "GYP already up to date (r $svnrevision)"
    fi
    popd > /dev/null || err
}

function build-v8() {
    pushd v8 > /dev/null || err
   
    if [[ "$PLATFORM" -eq "x64" ]] ; then
        makecall="x64.$CONFIGURATION"
    elif [[ "$PLATFORM" -eq "x86" ]] ; then
        makecall="ia32.$CONFIGURATION"
    else
        echo "Unsupported platform $PLATFORM."
        exit 1
    fi
    make $makecall library=shared || err

    pushd out/$makecall/lib.target > /dev/null
    cp libv8.so ../../../../src/EventStore/libs || err
    popd > /dev/null

    [[ -d ../src/EventStore/libs/include ]] || mkdir ../src/EventStore/libs/include

    pushd include > /dev/null || err
    cp *.h ../../src/EventStore/libs/include || err
    popd > /dev/null || err

    popd > /dev/null || err
}

function build-js1() {
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

    g++ $includeString $libsString *.cpp -o $outputDir/libjs1.so $gccArch -lv8 -O2 -fPIC --shared --save-temps || err
    popd > /dev/null || err
}

function patch-versionfiles {
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
        sed -i "/$assemblyVersionPattern/c$newAssemblyVersion" $file
        sed -i "/$assemblyFileVersionPattern/c$newAssemblyFileVersion" $file
        sed -i "/$assemblyVersionInformationalPattern/c$newAssemblyVersionInformational" $file
        sed -i "/$assemblyProductNamePattern/c$newAssemblyProductName" $file
        sed -i "/$assemblyCopyrightPattern/c$newAssemblyCopyright" $file
        sed -i "/$assemblyCompanyPattern/c$newAssemblyCompany" $file

        if grep "AssemblyInformationalVersion" $file > /dev/null ; then
            echo "Patched $file with version information"
        else
            echo $newAssemblyVersionInformational >> $file
            echo "Patched $file with version information"
        fi
    done
}

function revert-versionfiles {
    files=$( find . -name "AssemblyInfo.cs" )

    for file in $files
    do
        git checkout $file
        echo "Reverted $file"
    done
}

function build-eventstore {
    patch-versionfiles
    rm -rf bin/
    xbuild src/EventStore/EventStore.sln /p:Platform="Any CPU" /p:Configuration="$CONFIGURATION" || err
    revert-versionfiles
}

function clean-all {
    rm -rf bin/
    rm -f src/EventStore/libs/libv8.so
    rm -f src/EventStore/libs/libjs1.so
    pushd v8 > /dev/null
    git clean --quiet -e gyp -fdx -- build
    git clean --quiet -dfx -- src
    git clean --quiet -dfx -- test
    git clean --quiet -dfx -- tools
    git clean --quiet -dfx -- preparser
    git clean --quiet -dfx -- samples
    git reset --quiet --hard
    popd > /dev/null
    pushd src/EventStore/EventStore.Projections.v8Integration > /dev/null
    git clean --quiet -dfx -- .
    popd > /dev/null
}

checkParams $1 $2 $3

echo "Running from base directory: $BASE_DIR"

if [[ "$ACTION" == "full" ]] ; then
    clean-all
fi

if [[ "$ACTION" == "incremental" ]] ; then
    get-v8 $V8_TAG
    get-dependencies

    build-v8
    build-js1
    build-eventstore
else
    [[ -f src/EventStore/libs/libv8.so ]] || echo "Cannot find libv8.so - cannot do a quick build!"
    [[ -f src/EventStore/libs/libjs1.so ]] || echo "Cannot find libjs1.so - cannot do a quick build!"

    build-eventstore
fi
