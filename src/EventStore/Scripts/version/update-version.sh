#!/bin/bash
 
function err() {
  echo Failed! ${1:-"Unknown error"}
  if [ -e "$_es_tmpfile" ] ; then rm "$_es_tmpfile"; fi
  if [ -e "$_esca_tmpfile" ] ; then rm "$_esca_tmpfile"; fi
  exit 1
}

MSBuildProjectDirectory=$1
_es_tmpfile=`mktemp -d /tmp/esversion.XXXX`
_esca_tmpfile=`mktemp -d /tmp/escaversion.XXXX`

if [ ! -e "$MSBuildProjectDirectory/../EventStore.Common/Properties/ESVersion.txt" ] ; then err "No ESVersion.txt file found with current version!"; fi
if [ ! -e "$MSBuildProjectDirectory/../EventStore.ClientAPI/Properties/ESCAVersion.txt" ] ; then err "No ESCAVersion.txt file found with current version!"; fi

_es_version=`cat "$MSBuildProjectDirectory/../EventStore.Common/Properties/ESVersion.txt"`
_esca_version=`cat "$MSBuildProjectDirectory/../EventStore.ClientAPI/Properties/ESCAVersion.txt"`
_es_branch=`git rev-parse --abbrev-ref HEAD`
_es_log=`git log -n1 --pretty=format:"%H@%aD" HEAD`

echo '[assembly: System.Reflection.AssemblyVersion("'"$_es_version.0"'")]' > "$_es_tmpfile"
echo '[assembly: System.Reflection.AssemblyFileVersion("'"$_es_version.0"'")]' >> "$_es_tmpfile"
echo '[assembly: System.Reflection.AssemblyInformationalVersion("'"$_es_version.$_es_branch@$_es_log"'")]' >> "$_es_tmpfile"

echo '[assembly: System.Reflection.AssemblyVersion("'"$_esca_version.0"'")]' > "$_esca_tmpfile"
echo '[assembly: System.Reflection.AssemblyFileVersion("'"$_esca_version.0"'")]' >> "$_esca_tmpfile"
echo '[assembly: System.Reflection.AssemblyInformationalVersion("'"$_esca_version.$_es_branch@$_es_log"'")]' >> "$_esca_tmpfile"

if diff "$_es_tmpfile" "$MSBuildProjectDirectory/../EventStore.Common/Properties/AssemblyVersion.cs" >/dev/null ; then
   echo "Skip, ES version is the same"
else
   cp -f "$_es_tmpfile" "$MSBuildProjectDirectory/../EventStore.Common/Properties/AssemblyVersion.cs"
fi

if diff "$_esca_tmpfile" "$MSBuildProjectDirectory/../EventStore.ClientAPI/Properties/AssemblyVersion.cs" >/dev/null ; then
   echo "Skip, ES ClientAPI version is the same"
else
   cp -f "$_esca_tmpfile" "$MSBuildProjectDirectory/../EventStore.ClientAPI/Properties/AssemblyVersion.cs"
fi

if [ -e "$_es_tmpfile" ] ; then rm "$_es_tmpfile"; fi
if [ -e "$_esca_tmpfile" ] ; then rm "$_esca_tmpfile"; fi
