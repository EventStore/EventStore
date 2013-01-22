#!/bin/bash

function err() {
  echo FAILED.  See other messages
  exit
}

function get-v8() {
    if [[ -d v8 ]] ; then
       git pull || err
    else
       git clone git://github.com/v8/v8.git v8 || err
    fi
    pushd v8 || err
    git checkout $1 || err
    popd || err
}


function get-dependencies() {
    pushd v8 || err
    make dependencies || err
    popd || err
}


pushd $(dirname $0) || err
cd ../.. || err

# 3.16.7
get-v8 ba55532e3bfbdccec1f5e09a420aad61e1f1a287 || err
get-dependencies || err

popd || er


