##!/usr/bin/env bash
EVENTSTORE_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
MONO_DIR=`which mono`
exec $MONO_DIR EventStore.ClusterNode.exe $@
