#!/usr/bin/env bash
EVENTSTORE_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
MONO_PATH=`which mono`
LD_LIBRARY_PATH=${EVENTSTORE_DIR}:$LD_LIBRARY_PATH MONO_GC_DEBUG=clear-at-gc $MONO_PATH ${EVENTSTORE_DIR}/EventStore.ClusterNode.exe $@
