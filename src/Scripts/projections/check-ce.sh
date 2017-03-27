#!/bin/bash

curl -i -X POST -d '

fromAll().when({
  $any:function (state, ev) {
    if (state.c === undefined) state.c = 0;
    if (ev.streamId == "CE") {
      if (state.c != ev.sequenceNumber)
        throw "stream CE:: state.c: " + state.c + " seq: " + ev.sequenceNumber + " " + JSON.stringify(ev);
      state.c++;
    }
    return state;
  }}
);     

' http://127.0.0.1:2113/projections/persistent?name=checkCE\&type=JS

