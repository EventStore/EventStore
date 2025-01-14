#!/usr/bin/env bash
#This script can be executed on linux and mac to create EventStore packages

set -o errexit
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
base_directory="$script_dir/../"

function usage {
	echo "Usage:"
	echo "  $0 [<semantic_version=0.0.0.0>] [<runtime_list="">] [<distro_list="">]"
    echo "  Example:"
    echo "  $0 5.0.0-rc1 el7 CentOS-7"
    echo "  $0 5.0.0-rc1 ubuntu.16.04-x64,ubuntu.18.04-x64 Ubuntu-16.04,Ubuntu-18.04"
}

function clean_up {
    #clean up all files except the "packages" directory
    pushd "$base_directory" &>/dev/null
    git reset --hard
    git clean -xdf . -e packages
    git submodule foreach --recursive 'git reset --hard'
    git submodule foreach --recursive 'git clean -xdf'
    popd &>/dev/null
}

function get_package_type {
    local distro=$1
    if [[ "$distro" == "Ubuntu-16.04" ]]; then
        echo "deb"
    elif [[ "$distro" == "Ubuntu-18.04" ]]; then
        echo "deb"
    elif [[ "$distro" == "CentOS-7" ]]; then
        echo "rpm"
    elif [[ "$distro" == "AmazonLinux-2" ]]; then
        echo "rpm"
    elif [[ "$distro" == "OracleLinux-7" ]]; then
            echo "rpm"
    elif [[ "$distro" == "macOS" ]]; then
        echo "pkg"
    fi
}

function get_distro_tag {
    local distro=$1
    if [[ "$distro" == "Ubuntu-16.04" ]]; then
        echo "xenial"
    elif [[ "$distro" == "Ubuntu-18.04" ]]; then
        echo "bionic"
    elif [[ "$distro" == "CentOS-7" ]]; then
        echo "el7"
    elif [[ "$distro" == "AmazonLinux-2" ]]; then
        echo "amzn2"
    elif [[ "$distro" == "OracleLinux-7" ]]; then
        echo "ol7"
    elif [[ "$distro" == "macOS" ]]; then
        echo "macOS"
    fi
}

function get_distro_shortname {
    local distro=$1
    if [[ "$distro" == "Ubuntu-16.04" ]]; then
        echo "ubuntu"
    elif [[ "$distro" == "Ubuntu-18.04" ]]; then
        echo "ubuntu"
    elif [[ "$distro" == "CentOS-7" ]]; then
        echo "el7"
    elif [[ "$distro" == "AmazonLinux-2" ]]; then
        echo "el7"
    elif [[ "$distro" == "OracleLinux-7" ]]; then
        echo "el7"
    elif [[ "$distro" == "macOS" ]]; then
        echo "macos"
    fi
}

function build_server {
    local semantic_version=$1
    local assembly_version=$2
    local build_type=$3
    local configuration=$4
    local build_ui=$5

    clean_up

    for i in "${!runtime_list[@]}"
    do
    :
    local distro=${distro_list[$i]}

    distro_shortname=$(get_distro_shortname "$distro")

    echo "Building EventStore server ($build_type) for ${runtime_list[$i]}"

    if [[ "$build_type" == "oss" ]] ; then
        pushd oss &> /dev/null
        ./build.sh "$assembly_version" "$configuration" "${runtime_list[$i]}" "$build_ui"
        popd &> /dev/null
    elif [[ "$build_type" == "commercial" ]] ; then
        ./build.sh "$assembly_version" "$configuration" "${runtime_list[$i]}" "$build_ui"
    fi

    ./scripts/package-dotnet/package-dotnet.sh "$semantic_version" "$build_type" "${runtime_list[$i]}" "$distro" "$distro_shortname"

    package_type=$(get_package_type "$distro")
    distro_tag=$(get_distro_tag "$distro")

    if [[ "$package_type" == "deb" ]] ; then
        pushd "$base_directory/scripts/package-deb/$build_type"
        rake PACKAGE_NAME="eventstore-$build_type" VERSION="$semantic_version" ITERATION="1" DISTRO_NAME="$distro" DISTRO_TAG="$distro_tag"
        mkdir -p "$base_directory/packages/$distro_shortname/$distro_tag"
        local outputPackage="eventstore-${build_type}_${semantic_version}-1_amd64.deb"
        mv ./*.deb "$base_directory/packages/$distro_shortname/$distro_tag/$outputPackage"
        popd &> /dev/null
    elif [[ "$package_type" == "rpm" ]] ; then
        pushd "$base_directory/scripts/package-rpm/$build_type" &> /dev/null
        rake PACKAGE_NAME="eventstore-$build_type" VERSION="$semantic_version" ITERATION="1" DISTRO_NAME="$distro" DISTRO_TAG="$distro_tag"
        mkdir -p "$base_directory/packages/$distro_shortname/$distro_tag"
        local outputPackage="eventstore-${build_type}-${semantic_version}-1.${distro_tag}.x86_64.rpm"
        mv ./*.rpm "$base_directory/packages/$distro_shortname/$distro_tag/$outputPackage"
        popd &> /dev/null
    elif [[ "$package_type" == "pkg" ]] ; then
        pushd "$base_directory/scripts/package-osx/$build_type" &> /dev/null
        rake PACKAGE_NAME="eventstore-$build_type" VERSION="$semantic_version"
        mkdir -p "$base_directory/packages/$distro_shortname/$distro_tag"
        local outputPackage="eventstore-${build_type}-${semantic_version}.${distro_tag}.pkg"
        mv ./*.pkg "$base_directory/packages/$distro_shortname/$distro_tag/$outputPackage"
        popd &> /dev/null
    fi

    done
}

function get_assembly_version {
    local semantic_version=$1
    p1=$(echo "$semantic_version"|tr '-' '.'|cut -d '.' -f 1)
    p2=$(echo "$semantic_version"|tr '-' '.'|cut -d '.' -f 2)
    p3=$(echo "$semantic_version"|tr '-' '.'|cut -d '.' -f 3)
    echo "$p1.$p2.$p3.0"
}

if [[ "$1" == "-help" || "$1" == "--help" ]] ; then
    usage
    exit
fi

pushd "$base_directory" &> /dev/null

semantic_version=$1
IFS=',' read -ra runtime_list <<< "$2"
IFS=',' read -ra distro_list <<< "$3"

if [[ "$semantic_version" == "" ]] ; then
    semantic_version="0.0.0.0"
fi

if [[ "${#distro_list[@]}" -ne "${#runtime_list[@]}" ]]; then
    echo "The number of items in runtime_list does not match number of items in distro_list."
    exit 1
fi

assembly_version=$(get_assembly_version "$semantic_version")
configuration="Release"
build_ui="yes"

rm -rf ./packages &> /dev/null
mkdir ./packages

build_server "$semantic_version" "$assembly_version" "oss" "$configuration" "$build_ui"
# build_server "$semantic_version" "$assembly_version" "commercial" "$configuration" "$build_ui"

clean_up

popd &> /dev/null

echo "Done!"
