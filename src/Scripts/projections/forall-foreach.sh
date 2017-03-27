#!/bin/bash

curl -i -X POST -d '

if (typeof String.prototype.startsWith != "function") {
  String.prototype.startsWith = function (str){
    return this.slice(0, str.length) == str;
  };
};

fromAll().foreachStream().when({
  $any:function (state, ev) {
    if (state.c === undefined) state.c = 0;
    if (ev.streamId.startsWith("account")) {
      if (state.c != ev.sequenceNumber)
        throw "stream: " + ev.streamId + " state.c: " + state.c + " seq: " + ev.sequenceNumber;
      state.c++;
    }
    return state;
  }}
);     

' http://127.0.0.1:2113/projections/persistent?name=forall-foreach2\&type=JS

