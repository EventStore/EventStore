#!/bin/bash

function err() {
    exit 
}

pushd $(dirname $0)/../.. || err
js=$(pwd -P)
include="-I $js/libs/include"
libs="-L $js/libs"


pushd $js/EventStore.Projections.v8Integration/ || err
  if [[ ! -d x64/Debug ]] ; then
	  mkdir -p x64/Debug || err
  fi
  g++ $include $libs *.cpp -o $js/../../src/Public/libs/libjs1.so -lv8 -fPIC -shared --save-temps || err    


popd || err
popd || err
