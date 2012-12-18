#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

echo "Mono section"
apt-get install -y git-core || err
apt-get install -y libtool || err
apt-get install -y autoconf || err
apt-get install -y automake || err
apt-get install -y gcc || err
apt-get install -y build-essential || err
apt-get install -y gettext || err
apt-get install -y mono-runtime || err
apt-get install -y mono-gmcs || err

echo "v8 section"
apt-get install -y subversion || err
apt-get install -y g++ || err

echo "system section"
apt-get install -y ntp

echo "Done"