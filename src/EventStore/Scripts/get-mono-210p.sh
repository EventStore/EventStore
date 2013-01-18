#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

curdir=`dirname $0`

org="$curdir/mono/0001-ES-patch-for2-10.patch"
resolved=$(readlink -f $org)
export patchtoapply="$resolved"

$curdir/get-mono "mono-2-10" || err
