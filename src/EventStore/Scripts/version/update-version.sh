#!/bin/bash

function err() {
  echo Failed! ${1:-"Unknown error"}
  if [ -e "/tmp/es-ver.tmp" ] ; then rm /tmp/es-ver.tmp; fi
  popd
  exit 1
}

pushd $MSBuildProjectDirectory

MSBuildProjectDirectory=$1

if [ ! -e $MSBuildProjectDirectory/Properties/ESVersion.txt ] ; then err "No ESVersion.txt file found with current version!"; fi
if [ ! -e $MSBuildProjectDirectory/Properties/AssemblyVersion.cs ] ; then err "No AssemblyVersion.cs file found in EventStore.Common!"; fi

_es_version=`cat $MSBuildProjectDirectory/Properties/ESVersion.txt` || err "Read version"
_es_branch=`git rev-parse --abbrev-ref HEAD` || err "Read branch"
_es_hashtag=`git rev-parse HEAD` || err "Get hashtag"
_es_timestamp=`git log HEAD -n1 --pretty="%aD"` || err "Get timestamp"

if [ ! -n "$_es_branch" ] ; then err "Empty branch variable"; fi;
if [ ! -n "$_es_hashtag" ] ; then err "Empty hashtag variable"; fi;
if [ ! -n "$_es_timestamp" ] ; then err "Empty timestamp variable"; fi;

echo '[assembly: System.Reflection.AssemblyVersion("'"$_es_version.0"'")]' > /tmp/es-ver.tmp || err "Write es-ver.tmp"
echo '[assembly: System.Reflection.AssemblyFileVersion("'"$_es_version.0"'")]' >> /tmp/es-ver.tmp || err "Write es-ver.tmp"
echo '[assembly: System.Reflection.AssemblyInformationalVersion("'"$_es_version.$_es_branch@$_es_hashtag@$_es_timestamp"'")]' >> /tmp/es-ver.tmp || err "Write es-ver.tmp"

if diff /tmp/es-ver.tmp $MSBuildProjectDirectory/Properties/AssemblyVersion.cs >/dev/null ; then
   echo "Skip, version is the same"
else
   cp -f /tmp/es-ver.tmp $MSBuildProjectDirectory/Properties/AssemblyVersion.cs || err "Failed to copy AssemblyVersion.cs"
fi

if [ -e "/tmp/es-ver.tmp" ] ; then rm /tmp/es-ver.tmp; fi

echo "Done."
popd