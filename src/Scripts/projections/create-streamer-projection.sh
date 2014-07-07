#!/bin/bash

body='

fromCategory("chat").when({

  ChatMessage: function (s, ev) {
    
    return {};
  }

});
'

curl -i -X PUT -d "$body" http://127.0.0.1:2113/projection/streamer/query?type=JS

