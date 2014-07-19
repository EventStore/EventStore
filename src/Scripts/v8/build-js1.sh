#!/bin/bash

function err() {
    exit 1
}

pushd $(dirname $0)/../.. || err
js=$(pwd -P)
include="-I $js/libs/include"
libs="-L $js/libs"
output="$js/libs"


pushd $js/EventStore.Projections.v8Integration/ || err
  g++ $include $libs *.cpp -o $output/libjs1.so -lv8 -fPIC -shared --save-temps || err    


popd || err
popd || err
