#!/usr/bin/env bash

SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BASEDIR=$SCRIPTDIR/../..

DOCUMENTATIONOUTPUT_PATH=$SCRIPTDIR/documentation.md
DOCUMENTATIONGEN_PATH="$BASEDIR/tools/documentation-generation/EventStore.Documentation.exe"

EVENTSTORECLUSTERNODE_PATH=$BASEDIR/bin/clusternode

$DOCUMENTATIONGEN_PATH --event-store-binary-paths=$EVENTSTORECLUSTERNODE_PATH --output-path=$DOCUMENTATIONOUTPUT_PATH
