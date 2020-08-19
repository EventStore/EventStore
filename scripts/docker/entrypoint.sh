#!/usr/bin/env bash
set -e

if [ ! -z ${EVENTSTORE_CLUSTER_SIZE+x} ] && [ -z ${EVENTSTORE_INT_IP} ]
then
    export EVENTSTORE_INT_IP=`ip addr show eth0|grep -E '(\/[0-9][0-9])'|awk '/inet / {print $2}'|sed -e 's/\/.*//'`
fi

exec eventstored
