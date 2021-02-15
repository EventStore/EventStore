#!/usr/bin/env bash

docker run --volume $(pwd):/protolock -w /protolock nilslice/protolock "$@" --strict --protoroot /protolock/src/Protos
