#!/usr/bin/env bash

SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BASEDIR=$SCRIPTDIR/../..

DOCUMENTATIONOUTPUT_PATH=$SCRIPTDIR/documentation.md
DOCUMENTATIONGEN_PATH="$BASEDIR/tools/documentation-generation/EventStore.Documentation.exe"

EVENTSTORECLUSTERNODE_PATH=$BASEDIR/bin/clusternode

$DOCUMENTATIONGEN_PATH --eventStoreBinaryPaths=$EVENTSTORECLUSTERNODE_PATH --outputPath=$DOCUMENTATIONOUTPUT_PATH
