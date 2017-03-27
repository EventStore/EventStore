#!/bin/bash

curl -i -X POST -d '

fromAll().when({
  $any:function (state, ev) {
    if (state.c === undefined) state.c = 0;
    state.c++;
    emit("CE", "V", {v: state.c});
    return state;
  }}
);     

' http://127.0.0.1:2113/projections/persistent?name=genCE\&type=JS

