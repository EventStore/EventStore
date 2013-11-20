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

get-v8 3.17.11 || err
get-dependencies || err

popd || er


