#!/bin/bash

function err() {
    exit
}

function build-v8() {
    make x64 library=shared || err
}

function copy-files() {

  cp out/x64.debug/lib.target/* -t ../libs || err
  
  mkdir ../libs/include
  cp include/* -t ../libs/include || err
}


pushd $(dirname $0)/../../v8 || err
  build-v8 || err
  copy-files || err
popd || err


