#/bin/bash

function usage() {
    echo "Usage: $0 <versionfilename>"
    echo ""
    echo " versionfilename - The AssemblyInfo.cs file to revert to"
    echo "                   source control state"
    exit 1
}

function err() {
    echo "Error - see previous messages"
    exit 1
}

[[ $# -eq 1 ]] || usage

git checkout $1 || err
