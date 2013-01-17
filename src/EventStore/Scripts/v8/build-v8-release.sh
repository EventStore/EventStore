#!/bin/bash

function err() {
    exit
}

function build-v8() {
    make x64.release library=shared || err
}

function copy-files() {

  cp out/x64.release/lib.target/* -t ../libs || err

}


pushd $(dirname $0)/../../v8 || err
  build-v8 || err
  copy-files || err
popd || err
