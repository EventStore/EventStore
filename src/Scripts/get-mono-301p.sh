#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

curdir=`dirname $0`

org="$curdir/mono/0001-ES-patch.patch"
resolved=$(readlink -f $org)
export patchtoapply="$resolved"

$curdir/get-mono "mono-3.0.1" || err
