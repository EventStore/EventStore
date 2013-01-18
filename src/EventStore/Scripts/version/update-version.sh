#!/bin/bash
 
function err() {
  echo Failed! ${1:-"Unknown error"}
  if [ -e "/tmp/es-ver.tmp" ] ; then rm /tmp/es-ver.tmp; fi
  exit 1
}

MSBuildProjectDirectory=$1

if [ ! -e $MSBuildProjectDirectory/Properties/ESVersion.txt ] ; then err "No ESVersion.txt file found with current version!"; fi

_es_version=`cat $MSBuildProjectDirectory/Properties/ESVersion.txt`
_es_branch=`git rev-parse --abbrev-ref HEAD`
_es_log=`git log -n1 --pretty=format:"%H@%aD" HEAD`

echo '[assembly: System.Reflection.AssemblyVersion("'"$_es_version.0"'")]' > /tmp/es-ver.tmp
echo '[assembly: System.Reflection.AssemblyFileVersion("'"$_es_version.0"'")]' >> /tmp/es-ver.tmp
echo '[assembly: System.Reflection.AssemblyInformationalVersion("'"$_es_version.$_es_branch@$_es_log"'")]' >> /tmp/es-ver.tmp

if diff /tmp/es-ver.tmp $MSBuildProjectDirectory/Properties/AssemblyVersion.cs >/dev/null ; then
   echo "Skip, version is the same"
else
   cp -f /tmp/es-ver.tmp $MSBuildProjectDirectory/Properties/AssemblyVersion.cs
fi

if [ -e "/tmp/es-ver.tmp" ] ; then rm /tmp/es-ver.tmp; fi
