#!/bin/bash

function err() {
  echo FAIL!
  exit 1
}

curdir=`dirname $0`

cd $curdir || err

version=${1:-"DEVELOPMENT VERSION"}
branch=`git rev-parse --abbrev-ref HEAD` || err
hashtag=`git rev-parse HEAD` || err

cd ../../EventStore.Common/Version || err

cp -f Version.template Version.cs || err

sed 's/'"<VERSION>"'/'"$version"'/' -i "Version.cs" || err
sed 's/<BRANCH>/'"$branch"'/' -i "Version.cs" || err
sed 's/'"<HASHTAG>"'/'"$hashtag"'/' -i "Version.cs" || err

echo Done.
