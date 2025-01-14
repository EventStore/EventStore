#!/usr/bin/env bash
EVENTSTORE_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
LD_LIBRARY_PATH=${EVENTSTORE_DIR}:$LD_LIBRARY_PATH ${EVENTSTORE_DIR}/eventstored $@
