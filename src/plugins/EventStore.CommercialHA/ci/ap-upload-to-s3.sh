#!/usr/bin/env bash

set -o errexit
set -o pipefail
set -o xtrace

s3_bucket=$1
semantic_version=$2
build_id=$3
build_directory=$4

aws s3 cp "packages/$build_directory" "s3://$s3_bucket/$semantic_version/$build_id/$build_directory" --recursive