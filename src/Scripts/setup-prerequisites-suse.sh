#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

echo "Mono section"
zypper install -y git-core || err
zypper install -y libtool || err
zypper install -y autoconf || err
zypper install -y automake || err
zypper install -y gcc || err
#zypper install -y build-essential || err
zypper install -y -t pattern devel_C_C++ || err
zypper install -y gettext || err
#zypper install -y mono-runtime || err
#zypper install -y mono-gmcs || err
zypper install -y mono || err

echo "v8 section"
zypper install -y subversion || err
zypper install -y gcc-c++ || err

echo "system section"
zypper install -y ntp

echo "Done"