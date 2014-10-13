#!/usr/bin/env bash

version=$1
customMonoPrefix=$2

SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

function usage {
	echo "Usage:"
	echo "  $0 version monoprefix"
	echo ""
	echo "Note: By default, mono is assumed to be at the output of 'which mono'../lib"
    echo "      though it may be desirable to specify an actual location."
	exit
}

function writeLog {
	message=$1
	echo "[package-mono.sh] - $message"
}

if [[ "$version" == "" ]] ; then
	VERSIONSTRING="0.0.0.0"
	writeLog "Version defaulted to: 0.0.0.0"
else
	VERSIONSTRING=$version
	writeLog "Version set to: $VERSIONSTRING"
fi

if [[ "$customMonoPrefix" == "" ]]; then
    monopath=`which mono`
    toremove="/bin/mono"
    MONOPREFIX=${monopath:0:${#monopath}-${#toremove}}
    writeLog "Mono prefix defaulted to: $MONOPREFIX"
else
    MONOPREFIX=$customMonoPrefix
    writeLog "Mono prefix set to: $MONOPREFIX"
fi

MKBUNDLEPATH=$MONOPREFIX/bin/mkbundle
if [[ -f $MKBUNDLEPATH ]] ; then
	writeLog "Using mkbundle: $MKBUNDLEPATH"
else
	writeLog "Cannot find mkbundle"
	exit 1
fi

MACHINECONFIG=$MONOPREFIX/etc/mono/4.0/machine.config
if [[ -f $MACHINECONFIG ]] ; then
    writeLog "Using --machine-config: $MACHINECONFIG"
else
    writeLog "Cannot find machine config at $MACHINECONFIG"
    exit 1
fi

OS=`uname`
if [[ $OS == "Darwin" ]] ; then
    for SUBVER in 11 10 9 8
    do
        sdkpath=`xcodebuild -sdk -version | grep "MacOSX10.$SUBVER" | tail -1`
        if [[ $sdkpath != "" ]] ; then
            break
        fi
    done

    if [[ $sdkpath == "" ]] ; then
        writeLog "Can't find a MacOS SDK using xcodebuild -sdk -version"
        exit 1
    fi

    isysroot=${sdkpath:6}
    ES_COMPILE_FLAGS="-lobjc -liconv -framework CoreFoundation -isysroot $isysroot -I $MONOPREFIX/include/mono-2.0 -Wall"

    writeLog "Using MacOS Compile Flags: $ES_COMPILE_FLAGS"
fi

MONOCONFIG=$MONOPREFIX/etc/mono/config
if [[ -f $MONOCONFIG ]] ; then
    writeLog "Using --config: $MONOCONFIG"
else
    writeLog "Cannot find mono config at $MONOCONFIG"
    exit 1
fi

GCCPATH=`which gcc`
if [[ $? != 0 ]] ; then
	writeLog "Cannot find gcc"
	exit 1
else
	writeLog "Using gcc: $GCCPATH"
fi

OUTPUTDIR="$SCRIPTDIR/../../bin/packaged"
[[ -d $OUTPUTDIR ]] || mkdir -p $OUTPUTDIR


if [[ $OS == "Darwin" ]] ; then
    soext="dylib"
    PACKAGENAME="EventStore-OSS-Mac-v$VERSIONSTRING"
else
    soext="so"
    PACKAGENAME="EventStore-OSS-Linux-v$VERSIONSTRING"
fi

PACKAGEDIRECTORY="$OUTPUTDIR/$PACKAGENAME"

if [[ -d $PACKAGEDIRECTORY ]] ; then
    rm -rf $PACKAGEDIRECTORY
fi
mkdir $PACKAGEDIRECTORY


pushd $SCRIPTDIR/../../bin/clusternode/

if [[ $OS == "Darwin" ]] ; then
    mkbundle -c -o clusternode.c -oo clusternode.a EventStore.ClusterNode.exe EventStore.Core.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Projections.Core.dll EventStore.ClusterNode.Web.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll EventStore.Web.dll Mono.Security.dll --static --deps --config $MONOCONFIG --machine-config $MACHINECONFIG
    gcc -mmacosx-version-min=10.6 -o clusternode $ES_COMPILE_FLAGS clusternode.c clusternode.a $MONOPREFIX/lib/libmonosgen-2.0.a $MONOPREFIX/lib/libMonoPosixHelper.a
else
    mkbundle -c -o clusternode.c -oo clusternode.a EventStore.ClusterNode.exe EventStore.Core.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Projections.Core.dll EventStore.ClusterNode.Web.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll EventStore.Web.dll Mono.Security.dll --static --deps --config $MONOCONFIG --machine-config $MACHINECONFIG
    cc -o clusternode -Wall `pkg-config --cflags monosgen-2` clusternode.c  `pkg-config --libs-only-L monosgen-2` -Wl,-Bstatic -lmonosgen-2.0 -Wl,-Bdynamic `pkg-config --libs-only-l monosgen-2 | sed -e "s/\-lmono-2.0 //"` clusternode.a
fi

cp -r clusternode-web $PACKAGEDIRECTORY/
cp -r es-common-web $PACKAGEDIRECTORY/
cp -r Prelude $PACKAGEDIRECTORY/
cp -r projections $PACKAGEDIRECTORY/
cp -r web-resources $PACKAGEDIRECTORY/
cp -r Users $PACKAGEDIRECTORY/
cp libjs1.$soext $PACKAGEDIRECTORY/
cp libv8.$soext $PACKAGEDIRECTORY/
cp libicui18n.$soext $PACKAGEDIRECTORY/
cp libicuuc.$soext $PACKAGEDIRECTORY/
cp clusternode $PACKAGEDIRECTORY/
cp NLog.config $PACKAGEDIRECTORY/
cp $SCRIPTDIR/run-node.sh $PACKAGEDIRECTORY/run-node.sh

popd

pushd $SCRIPTDIR/../../bin/testclient

if [[ $OS == "Darwin" ]] ; then
    mkbundle -c -o testclient.c -oo testclient.a EventStore.TestClient.exe EventStore.Core.dll EventStore.ClientAPI.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll --static --deps --config $MONOCONFIG --machine-config $MACHINECONFIG
    gcc -o testclient $ES_COMPILE_FLAGS testclient.c testclient.a $MONOPREFIX/lib/libmonosgen-2.0.a $MONOPREFIX/lib/libMonoPosixHelper.a
else
    mkbundle -c -o testclient.c -oo testclient.a EventStore.TestClient.exe EventStore.Core.dll EventStore.ClientAPI.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll --static --deps --config $MONOCONFIG --machine-config $MACHINECONFIG
    cc -o testclient -Wall `pkg-config --cflags monosgen-2` testclient.c  `pkg-config --libs-only-L monosgen-2` -Wl,-Bstatic -lmonosgen-2.0 -Wl,-Bdynamic `pkg-config --libs-only-l monosgen-2 | sed -e "s/\-lmono-2.0 //"` testclient.a
fi

cp testclient $PACKAGEDIRECTORY/

popd


pushd $OUTPUTDIR

tar -zcvf $PACKAGENAME.tar.gz $PACKAGENAME
rm -r $PACKAGEDIRECTORY

[[ -d ../../packages ]] || mkdir -p ../../packages
mv $PACKAGENAME.tar.gz ../../packages/

popd

rm -r $OUTPUTDIR
