#!/usr/bin/env bash

version=$1
configuration=$2

SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

function usage {
	echo "Usage:"
	echo "  $0 version configuration"
	echo ""
	echo "Note: configuration only specifies which directory the Event Store binaries"
	echo "      are stored in, and have no effect on the build/link of mono."
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

if [[ "$configuration" == "" ]]; then
	CONFIGURATION="release"
	writeLog "Configuration defaulted to: $CONFIGURATION"
else
	if [[ "$configuration" == "release" || "$configuration" == "debug" ]]; then
		CONFIGURATION=$configuration
		writeLog "Configuration set to: $CONFIGURATION"
	else
		writeLog "Invalid configuration: $configuration"
		usage
	fi
fi

MKBUNDLEPATH=`which mkbundle`
if [[ $? != 0 ]] ; then
	writeLog "Cannot find mkbundle"
	exit 1
else
	writeLog "Using mkbundle: $MKBUNDLEPATH"
fi

GCCPATH=`which gcc`
if [[ $? != 0 ]] ; then
	writeLog "Cannot find gcc"
	exit 1
else
	writeLog "Using gcc: $GCCPATH"
fi

pushd $DIR../../../../bin/eventstore/$CONFIGURATION/anycpu

mkbundle -c -o cluster.c -oo eventstorecluster.a EventStore.ClusterNode.exe EventStore.Core.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Projections.Core.dll EventStore.SingleNode.Web.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll EventStore.Web.dll Mono.Security.dll --static --deps --machine-config /opt/mono/etc/mono/4.0/machine.config

mkbundle -c -o singlenode.c -oo eventstoresingle.a EventStore.SingleNode.exe EventStore.Core.dll EventStore.BufferManagement.dll EventStore.Common.dll EventStore.Projections.Core.dll EventStore.SingleNode.Web.dll EventStore.Transport.Http.dll EventStore.Transport.Tcp.dll Newtonsoft.Json.dll NLog.dll protobuf-net.dll EventStore.Web.dll Mono.Security.dll --static --deps --machine-config /opt/mono/etc/mono/4.0/machine.config

cc -o clusternode -Wall `pkg-config --cflags monosgen-2` cluster.c  `pkg-config --libs-only-L monosgen-2` -Wl,-Bstatic -lmonosgen-2.0 -Wl,-Bdynamic `pkg-config --libs-only-l monosgen-2 | sed -e "s/\-lmono-2.0 //"` eventstorecluster.a

cc -o singlenode -Wall `pkg-config --cflags monosgen-2` singlenode.c  `pkg-config --libs-only-L monosgen-2` -Wl,-Bstatic -lmonosgen-2.0 -Wl,-Bdynamic `pkg-config --libs-only-l monosgen-2 | sed -e "s/\-lmono-2.0 //"` eventstoresingle.a

PACKAGEDIRECTORY="EventStore-Mono-v$VERSIONSTRING"
if [[ -d $PACKAGEDIRECTORY ]] ; then
	rm -rf $PACKAGEDIRECTORY
fi

mkdir $PACKAGEDIRECTORY

cp -r clusternode-web $PACKAGEDIRECTORY/
cp -r es-common-web $PACKAGEDIRECTORY/
cp -r singlenode-web $PACKAGEDIRECTORY/
cp -r Prelude $PACKAGEDIRECTORY/
cp -r web-resources $PACKAGEDIRECTORY/
cp -r Users $PACKAGEDIRECTORY/
cp singlenode-config.dist.json $PACKAGEDIRECTORY/
cp clusternode-config.dist.json $PACKAGEDIRECTORY/
cp libjs1.so $PACKAGEDIRECTORY/
cp libv8.so $PACKAGEDIRECTORY/
cp libicui18n.so $PACKAGEDIRECTORY/
cp libicuuc.so $PACKAGEDIRECTORY/
cp clusternode $PACKAGEDIRECTORY/
cp singlenode $PACKAGEDIRECTORY/
cp NLog.config $PACKAGEDIRECTORY/
cp $SCRIPTDIR/System.dll.config $PACKAGEDIRECTORY/System.dll.config
cp $SCRIPTDIR/clusternode.sh $PACKAGEDIRECTORY/run-clusternode.sh
cp $SCRIPTDIR/singlenode.sh $PACKAGEDIRECTORY/run-singlenode.sh

tar -zcvf $PACKAGEDIRECTORY.tar.gz $PACKAGEDIRECTORY

rm -rf $PACKAGEDIRECTORY

popd
