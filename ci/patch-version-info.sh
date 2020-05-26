#!/bin/bash

function patchVersionInfo {
    version=$1
    version_info=$2
    if [[ "$version" == "" ]] ; then
        $version="Unknown"
    fi

    commitRegex="Commit: ([^,]*)"
    tagRegex="Tag: '(.*)'"

    commit="Unknown"
    commitTimestamp="Unknown"
    tag="Unknown"

    if [[ $version_info =~ $commitRegex ]]
    then
        commit=${BASH_REMATCH[1]}
    fi

    if [[ $version_info =~ $tagRegex ]]
    then
        tag=${BASH_REMATCH[1]}
    fi

    newVersion="public static readonly string Version = \"$version\";"
    newBranch="public static readonly string Branch = \"$tag\";"
    newCommitHash="public static readonly string Hashtag = \"$commit\";"
    newTimestamp="public static readonly string Timestamp = \"$commitTimestamp\";"

    versionPattern="public static readonly string Version .*$"
    branchPattern="public static readonly string Branch .*$"
    commitHashPattern="public static readonly string Hashtag .*$"
    timestampPattern="public static readonly string Timestamp .*$"

    files=$( find . -name "VersionInfo.cs" )

    for file in $files
    do
        tempfile="$file.tmp"
        sed -e "s/$versionPattern/$newVersion/" \
            -e "s/$branchPattern/$newBranch/" \
            -e "s/$commitHashPattern/$newCommitHash/" \
            -e "s/$timestampPattern/$newTimestamp/" \
            "$file" > "$tempfile"

        cat "$tempfile"
        mv "$tempfile" "$file"
        echo "Patched $file with version information"
    done
}

patchVersionInfo "$1" "$2"