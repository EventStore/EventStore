#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

curdir=`dirname $0`

cd $curdir || err

cd ../../EventStore.Common/Version || err

cp -f Version.template Version.cs || err

echo Done.
