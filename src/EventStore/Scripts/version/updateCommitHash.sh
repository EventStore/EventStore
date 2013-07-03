#!/bin/bash

function usage() {
    echo "Usage: $0 <versionfilename>"
    echo ""
    echo " versionfilename - the path to the AssemblyInfo.cs file which is to"
    echo "                   be patched."
    echo ""
    exit 1
}

[[ $# -eq 1 ]] || usage

file=$1

branchName=`git rev-parse --abbrev-ref HEAD`
commitHashAndTime=`git log -n1 --pretty=format:"%H@%aD" HEAD`

# We'll only update the hash as part of the xbuild process if it's
# not been set (probably meaning xbuild has been called directly. We
# can tell that by the version being 0.0.0.0 - although this will be
# true for CI builds as well, the operation here is idempotent and
# will be backed out by the post build step.
assemblyVersionInformationalPattern='assembly: AssemblyInformationalVersion("\0\.0\.0\.0\..*'
newAssemblyVersionInformational="[assembly: AssemblyInformationalVersion(\"0.0.0.0.$branchName@$commitHashAndTime\")]"

tempfile="$file.tmp"

sed "/$assemblyVersionInformationalPattern/c\
    $newAssemblyVersionInformational" $1 > $tempfile

mv $tempfile $file

if grep "AssemblyInformationalVersion" $file > /dev/null ; then
    echo "Patched $file with commit hash information"
else
    echo " " >> $file
    echo $newAssemblyVersionInformational >> $file
    echo "Patched $file with commit hash information"
fi
