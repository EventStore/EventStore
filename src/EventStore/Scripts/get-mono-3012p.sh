#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

curdir=`dirname $0`

$curdir/get-mono-patched.sh "mono-3.0.122" || err
