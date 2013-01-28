#!/bin/sh

curdir=`dirname $0` || { echo Failed to get current dir; exit 1; }

export LD_LIBRARY_PATH="$curdir/eventstore:$LD_LIBRARY_PATH"

echo $LD_LIBRARY_PATH

mono-sgen $curdir/eventstore/EventStore.SingleNode.exe $* || exit 1;
